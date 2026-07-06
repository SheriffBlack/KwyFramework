using System.Collections.Concurrent;
using System.Diagnostics;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// Executes one state-monitored motion operation per axis.
/// </summary>
public sealed class AxisMotionExecutor : IAxisMotionExecutor, IDisposable
{
    private readonly IAxisMotionController controller;
    private readonly IMotionProfileController profileController;
    private readonly IMotionStateMonitor stateMonitor;
    private readonly IMotionSafetyGuard safetyGuard;
    private readonly ConcurrentDictionary<short, AxisOperation> activeOperations = new();
    private readonly ConcurrentDictionary<short, PositionCrossingWaiter> crossingWaiters = new();
    private bool disposed;

    public AxisMotionExecutor(
        IAxisMotionController controller,
        IMotionProfileController profileController,
        IMotionStateMonitor stateMonitor,
        IMotionSafetyGuard safetyGuard)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.profileController = profileController ?? throw new ArgumentNullException(nameof(profileController));
        this.stateMonitor = stateMonitor ?? throw new ArgumentNullException(nameof(stateMonitor));
        this.safetyGuard = safetyGuard ?? throw new ArgumentNullException(nameof(safetyGuard));
        stateMonitor.AxisSnapshotCaptured += OnAxisSnapshotCaptured;
    }

    public async Task<MotionCompletionResult> MoveAbsAsync(
        short axis,
        double position,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(profile);
        options ??= new MotionExecutionOptions();
        options.Validate();
        await EnsureMonitorStartedAsync().ConfigureAwait(false);

        MotionAxisSnapshot snapshot = stateMonitor.GetAxisSnapshot(axis);
        int direction = Math.Sign(position - snapshot.Position);
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Absolute, position, direction));
        if (Math.Abs(position - snapshot.Position) <= options.PositionTolerance)
        {
            return new(axis, position, snapshot.Position, options.PositionTolerance);
        }

        var operation = new PositionMotionOperation(this, axis, position, direction, options, cancellationToken);
        AddOperation(operation);
        try
        {
            if (!operation.IsCompleted)
            {
                profileController.MoveAbs(axis, position, profile);
            }
        }
        catch (Exception exception)
        {
            operation.TrySetException(exception);
        }

        return await operation.Task.ConfigureAwait(false);
    }

    public async Task<MotionCompletionResult> MoveRelAsync(
        short axis,
        double distance,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureMonitorStartedAsync().ConfigureAwait(false);
        double target = stateMonitor.GetAxisSnapshot(axis).Position + distance;
        return await MoveAbsAsync(axis, target, profile, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MotionAxisSnapshot> WaitForPositionCrossedAsync(
        short axis,
        double position,
        PositionCrossingDirection direction,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        ValidateTimeout(timeout);
        await EnsureMonitorStartedAsync().ConfigureAwait(false);
        MotionAxisSnapshot snapshot = stateMonitor.GetAxisSnapshot(axis);
        if (HasCrossed(snapshot.Position, position, direction))
        {
            return snapshot;
        }

        var waiter = new PositionCrossingWaiter(this, axis, position, direction, timeout, cancellationToken);
        if (!crossingWaiters.TryAdd(axis, waiter))
        {
            waiter.Dispose();
            throw new MotionOperationInProgressException(axis);
        }

        waiter.Observe(stateMonitor.GetAxisSnapshot(axis));
        return await waiter.Task.ConfigureAwait(false);
    }

    public async Task<SensorSeekResult> SeekSensorAsync(
        short axis,
        IIoCardDevice ioDevice,
        int channel,
        double velocity,
        SensorSeekOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(ioDevice);
        if (channel is < 0 or >= 64)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        if (!double.IsFinite(velocity) || velocity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(velocity));
        }

        options ??= new SensorSeekOptions();
        options.Validate();
        await EnsureMonitorStartedAsync().ConfigureAwait(false);
        safetyGuard.ValidateAndThrow(new(axis, MotionRequestKind.Jog, Direction: Math.Sign(velocity), RequiresHomed: false));

        var operation = new SensorSeekOperation(this, axis, channel, options, cancellationToken);
        AddOperation(operation);
        EventHandler<ulong>? handler = null;
        IHardwareInterruptSource? interruptSource = null;
        if (options.StopMode == SensorStopMode.ControllerHardwareStop)
        {
            handler = (_, mask) =>
            {
                bool state = (mask & (1UL << channel)) != 0;
                if (state == options.ExpectedState)
                {
                    operation.SignalSensor();
                }
            };
            interruptSource = ioDevice as IHardwareInterruptSource;
            if (interruptSource is not null)
            {
                interruptSource.OnHardwareTriggerReceived += handler;
            }
        }

        try
        {
            if (ioDevice.ReadDiBit(channel) == options.ExpectedState)
            {
                operation.SignalSensor();
            }
            else if (!operation.IsCompleted)
            {
                controller.MoveJog(axis, velocity);
            }

            if (!operation.IsCompleted)
            {
                _ = PollSensorAsync(ioDevice, operation, options.PollInterval);
            }

            return await operation.Task.ConfigureAwait(false);
        }
        catch (Exception exception) when (!operation.IsCompleted)
        {
            operation.TrySetException(exception);
            throw;
        }
        finally
        {
            if (handler is not null && interruptSource is not null)
            {
                interruptSource.OnHardwareTriggerReceived -= handler;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        stateMonitor.AxisSnapshotCaptured -= OnAxisSnapshotCaptured;
        foreach (AxisOperation operation in activeOperations.Values)
        {
            operation.TrySetException(new ObjectDisposedException(nameof(AxisMotionExecutor)));
        }

        foreach (PositionCrossingWaiter waiter in crossingWaiters.Values)
        {
            waiter.TrySetException(new ObjectDisposedException(nameof(AxisMotionExecutor)));
        }
    }

    private async Task PollSensorAsync(
        IIoCardDevice ioDevice,
        SensorSeekOperation operation,
        TimeSpan pollInterval)
    {
        using var timer = new PeriodicTimer(pollInterval);
        try
        {
            while (!operation.IsCompleted && await timer.WaitForNextTickAsync(operation.OperationToken).ConfigureAwait(false))
            {
                if (ioDevice.ReadDiBit(operation.Channel) == operation.ExpectedState)
                {
                    operation.SignalSensor();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (operation.IsCompleted)
        {
        }
        catch (Exception exception)
        {
            operation.TrySetException(exception);
        }
    }

    private Task EnsureMonitorStartedAsync()
        => stateMonitor.IsRunning ? Task.CompletedTask : stateMonitor.StartAsync(CancellationToken.None);

    private void AddOperation(AxisOperation operation)
    {
        if (!activeOperations.TryAdd(operation.Axis, operation))
        {
            operation.Dispose();
            throw new MotionOperationInProgressException(operation.Axis);
        }

        operation.Activate();
    }

    private void OnAxisSnapshotCaptured(MotionAxisSnapshot snapshot)
    {
        if (activeOperations.TryGetValue(snapshot.Axis, out AxisOperation? operation))
        {
            operation.Observe(snapshot);
        }

        if (crossingWaiters.TryGetValue(snapshot.Axis, out PositionCrossingWaiter? waiter))
        {
            waiter.Observe(snapshot);
        }
    }

    private void Remove(AxisOperation operation)
    {
        activeOperations.TryRemove(operation.Axis, out _);
    }

    private void Remove(PositionCrossingWaiter waiter)
    {
        crossingWaiters.TryRemove(waiter.Axis, out _);
    }

    private void StopSafely(short axis, bool abort)
    {
        try
        {
            if (abort)
            {
                controller.Abort(axis);
            }
            else
            {
                controller.Stop(axis);
            }
        }
        catch
        {
        }
    }

    private static bool HasCrossed(double current, double threshold, PositionCrossingDirection direction)
        => direction == PositionCrossingDirection.Positive ? current >= threshold : current <= threshold;

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private abstract class AxisOperation : IDisposable
    {
        private readonly AxisMotionExecutor owner;
        private readonly CancellationToken externalToken;
        private readonly TimeSpan timeout;
        private CancellationTokenSource? lifetimeCts;
        private CancellationTokenSource? timeoutCts;
        private CancellationTokenRegistration cancellationRegistration;
        private CancellationTokenRegistration timeoutRegistration;
        private int completed;

        protected AxisOperation(
            AxisMotionExecutor owner,
            short axis,
            TimeSpan timeout,
            CancellationToken externalToken)
        {
            this.owner = owner;
            Axis = axis;
            this.timeout = timeout;
            this.externalToken = externalToken;
        }

        public short Axis { get; }

        public bool IsCompleted => Volatile.Read(ref completed) != 0;

        public CancellationToken OperationToken => lifetimeCts?.Token ?? CancellationToken.None;

        protected CancellationToken ExternalToken => externalToken;

        public void Activate()
        {
            if (IsCompleted)
            {
                return;
            }

            lifetimeCts = new CancellationTokenSource();
            if (externalToken.CanBeCanceled)
            {
                cancellationRegistration = externalToken.Register(static state =>
                {
                    var operation = (AxisOperation)state!;
                    operation.OnCanceled();
                }, this);
            }

            if (timeout != Timeout.InfiniteTimeSpan)
            {
                timeoutCts = new CancellationTokenSource(timeout);
                timeoutRegistration = timeoutCts.Token.Register(static state =>
                {
                    ((AxisOperation)state!).OnTimeout();
                }, this);
            }
        }

        public abstract void Observe(MotionAxisSnapshot snapshot);

        public abstract void TrySetException(Exception exception);

        protected bool TryComplete()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
            {
                return false;
            }

            owner.Remove(this);
            lifetimeCts?.Cancel();
            Dispose();
            return true;
        }

        protected virtual void OnCanceled()
        {
            owner.StopSafely(Axis, abort: false);
            TrySetCanceled(externalToken);
        }

        protected virtual void OnTimeout()
        {
            owner.StopSafely(Axis, abort: false);
            TrySetException(new TimeoutException($"Axis {Axis} motion did not complete within {timeout}."));
        }

        protected abstract void TrySetCanceled(CancellationToken cancellationToken);

        public void Dispose()
        {
            cancellationRegistration.Unregister();
            timeoutRegistration.Unregister();
            timeoutCts?.Dispose();
            timeoutCts = null;
            lifetimeCts?.Dispose();
            lifetimeCts = null;
        }
    }

    private sealed class PositionMotionOperation : AxisOperation
    {
        private readonly AxisMotionExecutor owner;
        private readonly TaskCompletionSource<MotionCompletionResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly MotionExecutionOptions options;
        private bool observedMoving;

        public PositionMotionOperation(
            AxisMotionExecutor owner,
            short axis,
            double target,
            int direction,
            MotionExecutionOptions options,
            CancellationToken cancellationToken)
            : base(owner, axis, options.Timeout, cancellationToken)
        {
            this.owner = owner;
            Target = target;
            Direction = direction;
            this.options = options;
        }

        public double Target { get; }

        public int Direction { get; }

        public Task<MotionCompletionResult> Task => completion.Task;

        public override void Observe(MotionAxisSnapshot snapshot)
        {
            if (IsCompleted)
            {
                return;
            }

            observedMoving |= snapshot.IsMoving;
            Exception? failure = GetFailure(snapshot);
            if (failure is not null)
            {
                TrySetException(failure);
                return;
            }

            if (!snapshot.IsMoving && Math.Abs(snapshot.Position - Target) <= options.PositionTolerance && TryComplete())
            {
                completion.TrySetResult(new(Axis, Target, snapshot.Position, options.PositionTolerance));
            }
        }

        public override void TrySetException(Exception exception)
        {
            if (TryComplete())
            {
                completion.TrySetException(exception);
            }
        }

        protected override void TrySetCanceled(CancellationToken cancellationToken)
        {
            if (TryComplete())
            {
                completion.TrySetCanceled(cancellationToken);
            }
        }

        protected override void OnCanceled()
        {
            if (options.StopOnCancellation)
            {
                owner.StopSafely(Axis, abort: false);
            }

            TrySetCanceled(ExternalToken);
        }

        private Exception? GetFailure(MotionAxisSnapshot snapshot)
        {
            if (snapshot.IsAlarm)
            {
                return new MotionAlarmException(Axis);
            }

            if (!snapshot.IsServoEnabled)
            {
                return new MotionServoDisabledException(Axis);
            }

            if (Direction > 0 && snapshot.IsPositiveLimit)
            {
                return new MotionLimitException(Axis, positive: true);
            }

            if (Direction < 0 && snapshot.IsNegativeLimit)
            {
                return new MotionLimitException(Axis, positive: false);
            }

            if (!snapshot.IsMoving
                && Math.Abs(snapshot.Position - Target) > options.PositionTolerance
                && (observedMoving || stopwatch.Elapsed >= options.StartDetectionDelay))
            {
                return new MotionPositionException(Axis, Target, snapshot.Position, options.PositionTolerance);
            }

            return null;
        }
    }

    private sealed class SensorSeekOperation : AxisOperation
    {
        private readonly AxisMotionExecutor owner;
        private readonly TaskCompletionSource<SensorSeekResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SensorSeekOptions options;
        private int sensorTriggered;

        public SensorSeekOperation(
            AxisMotionExecutor owner,
            short axis,
            int channel,
            SensorSeekOptions options,
            CancellationToken cancellationToken)
            : base(owner, axis, options.Timeout, cancellationToken)
        {
            this.owner = owner;
            Channel = channel;
            this.options = options;
        }

        public int Channel { get; }

        public bool ExpectedState => options.ExpectedState;

        public Task<SensorSeekResult> Task => completion.Task;

        public void SignalSensor()
        {
            if (Interlocked.Exchange(ref sensorTriggered, 1) == 0
                && options.StopMode == SensorStopMode.SoftwareStop)
            {
                owner.StopSafely(Axis, abort: false);
            }

            if (owner.stateMonitor.GetAxisSnapshot(Axis) is { } snapshot)
            {
                Observe(snapshot);
            }
        }

        public override void Observe(MotionAxisSnapshot snapshot)
        {
            if (IsCompleted)
            {
                return;
            }

            if (snapshot.IsAlarm)
            {
                TrySetException(new MotionAlarmException(Axis));
                return;
            }

            if (!snapshot.IsServoEnabled)
            {
                TrySetException(new MotionServoDisabledException(Axis));
                return;
            }

            if (Volatile.Read(ref sensorTriggered) != 0 && !snapshot.IsMoving && TryComplete())
            {
                completion.TrySetResult(new(Axis, Channel, snapshot.EncoderPosition, options.StopMode));
            }
        }

        public override void TrySetException(Exception exception)
        {
            if (TryComplete())
            {
                completion.TrySetException(exception);
            }
        }

        protected override void TrySetCanceled(CancellationToken cancellationToken)
        {
            if (TryComplete())
            {
                completion.TrySetCanceled(cancellationToken);
            }
        }

        protected override void OnCanceled()
        {
            owner.StopSafely(Axis, abort: options.AbortOnCancellation);
            TrySetCanceled(ExternalToken);
        }
    }

    private sealed class PositionCrossingWaiter : IDisposable
    {
        private readonly AxisMotionExecutor owner;
        private readonly TaskCompletionSource<MotionAxisSnapshot> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly PositionCrossingDirection direction;
        private readonly double position;
        private readonly CancellationTokenSource? timeoutCts;
        private readonly CancellationTokenRegistration cancellationRegistration;
        private readonly CancellationTokenRegistration timeoutRegistration;
        private int completed;

        public PositionCrossingWaiter(
            AxisMotionExecutor owner,
            short axis,
            double position,
            PositionCrossingDirection direction,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            this.owner = owner;
            Axis = axis;
            this.position = position;
            this.direction = direction;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    var tuple = ((PositionCrossingWaiter Waiter, CancellationToken Token))state!;
                    tuple.Waiter.TrySetCanceled(tuple.Token);
                }, (this, cancellationToken));
            }

            if (timeout != Timeout.InfiniteTimeSpan)
            {
                timeoutCts = new CancellationTokenSource(timeout);
                timeoutRegistration = timeoutCts.Token.Register(static state =>
                    ((PositionCrossingWaiter)state!).TrySetException(new TimeoutException("Position crossing wait timed out.")), this);
            }
        }

        public short Axis { get; }

        public Task<MotionAxisSnapshot> Task => completion.Task;

        public void Observe(MotionAxisSnapshot snapshot)
        {
            if (HasCrossed(snapshot.Position, position, direction) && TryComplete())
            {
                completion.TrySetResult(snapshot);
            }
        }

        public void TrySetException(Exception exception)
        {
            if (TryComplete())
            {
                completion.TrySetException(exception);
            }
        }

        public void Dispose()
        {
            cancellationRegistration.Unregister();
            timeoutRegistration.Unregister();
            timeoutCts?.Dispose();
        }

        private void TrySetCanceled(CancellationToken cancellationToken)
        {
            if (TryComplete())
            {
                completion.TrySetCanceled(cancellationToken);
            }
        }

        private bool TryComplete()
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
            {
                return false;
            }

            owner.Remove(this);
            Dispose();
            return true;
        }
    }
}
