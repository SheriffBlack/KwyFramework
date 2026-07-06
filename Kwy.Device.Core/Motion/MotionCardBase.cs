using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 运动控制卡抽象基类
/// </summary>
public abstract class MotionCardBase :
    DeviceBase,
    IStandardMotionCard
{
    public IDeviceConfig Config => DeviceParameter;

    protected MotionCardBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
    }

    // ==========================================
    // IMotionCard 实现
    // ==========================================

    public abstract void ServoOn(short axis);
    public abstract void ServoOff(short axis);
    public abstract void ClearError(short axis);

    public abstract void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5);
    public abstract void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5);
    public virtual void MoveAbs(short axis, double position, MotionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        MoveAbs(axis, position, profile.Velocity, profile.Acceleration, profile.Deceleration);
    }

    public virtual void MoveRel(short axis, double distance, MotionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        MoveRel(axis, distance, profile.Velocity, profile.Acceleration, profile.Deceleration);
    }
    public abstract void MoveJog(short axis, double velocity);
    public abstract void Stop(short axis);
    public abstract void Abort(short axis);
    public abstract void GoHome(short axis);

    public abstract double GetPosition(short axis);
    public abstract double GetEncoderPosition(short axis);
    public abstract double GetVelocity(short axis);
    public abstract int GetStatus(short axis);
    public abstract bool IsMoving(short axis);
    public abstract bool IsPositiveLimit(short axis);
    public abstract bool IsNegativeLimit(short axis);
    public abstract bool IsAlarm(short axis);
    public abstract void SetSoftLimit(short axis, double positive, double negative);

    public virtual HomeStatus GetHomeStatus(short axis)
        => new(axis, HomeState.Unknown, 0);

    public virtual MotionAxisSnapshot GetAxisSnapshot(short axis)
    {
        return new MotionAxisSnapshot(
            axis,
            GetPosition(axis),
            GetEncoderPosition(axis),
            GetVelocity(axis),
            GetStatus(axis),
            IsMoving(axis),
            IsAlarm(axis),
            IsPositiveLimit(axis),
            IsNegativeLimit(axis),
            DateTimeOffset.Now,
            homeState: GetHomeStatus(axis).State);
    }

    public virtual Task WaitForAxisStoppedAsync(short axis, CancellationToken cancellationToken = default)
        => WaitForAxisStoppedAsync(axis, Timeout.InfiniteTimeSpan, cancellationToken);

    public virtual Task WaitForAxisStoppedAsync(short axis, TimeSpan timeout, CancellationToken cancellationToken = default)
        => WaitUntilAsync(
            () => !IsMoving(axis),
            timeout,
            () => IsAlarm(axis) ? new InvalidOperationException($"Axis {axis} entered alarm state while waiting for motion to stop.") : null,
            cancellationToken);

    public virtual async Task<MotionCompletionResult> WaitForAxisCompletedAsync(
        short axis,
        double targetPosition,
        double tolerance,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateCompletionArguments(targetPosition, tolerance);
        MotionAxisSnapshot snapshot = default;
        var startupGrace = System.Diagnostics.Stopwatch.StartNew();
        await WaitUntilAsync(
            () =>
            {
                snapshot = GetAxisSnapshot(axis);
                return !snapshot.IsMoving
                    && Math.Abs(snapshot.Position - targetPosition) <= tolerance;
            },
            timeout,
            () => GetMotionCompletionFailure(
                axis,
                targetPosition,
                tolerance,
                snapshot,
                allowStoppedFailure: startupGrace.Elapsed >= TimeSpan.FromMilliseconds(100)),
            cancellationToken).ConfigureAwait(false);

        return new MotionCompletionResult(axis, targetPosition, snapshot.Position, tolerance);
    }

    public virtual Task WaitForHomeCompletedAsync(short axis, CancellationToken cancellationToken = default)
        => WaitForHomeCompletedAsync(axis, Timeout.InfiniteTimeSpan, cancellationToken);

    public virtual async Task<HomeStatus> WaitForHomeCompletedAsync(
        short axis,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        HomeStatus status = default;
        await WaitUntilAsync(
            () =>
            {
                status = GetHomeStatus(axis);
                if (status.State == HomeState.Unknown && !IsMoving(axis))
                {
                    status = new HomeStatus(axis, HomeState.Succeeded, status.RawStatus);
                }

                return status.IsCompleted;
            },
            timeout,
            () => IsAlarm(axis)
                ? new MotionAlarmException(axis)
                : status.State == HomeState.Failed ? new MotionHomeException(status) : null,
            cancellationToken).ConfigureAwait(false);

        if (status.State == HomeState.Failed)
        {
            throw new MotionHomeException(status);
        }

        return status;
    }

    protected static Exception? GetMotionCompletionFailure(
        short axis,
        double targetPosition,
        double tolerance,
        MotionAxisSnapshot snapshot,
        bool allowStoppedFailure = true)
    {
        if (snapshot.IsAlarm)
        {
            return new MotionAlarmException(axis);
        }

        if (allowStoppedFailure && !snapshot.IsMoving)
        {
            if (snapshot.IsPositiveLimit)
            {
                return new MotionLimitException(axis, positive: true);
            }

            if (snapshot.IsNegativeLimit)
            {
                return new MotionLimitException(axis, positive: false);
            }

            if (Math.Abs(snapshot.Position - targetPosition) > tolerance)
            {
                return new MotionPositionException(axis, targetPosition, snapshot.Position, tolerance);
            }
        }

        return null;
    }

    protected static void ValidateCompletionArguments(double targetPosition, double tolerance)
    {
        if (!double.IsFinite(targetPosition))
        {
            throw new ArgumentOutOfRangeException(nameof(targetPosition), targetPosition, "Target position must be finite.");
        }

        if (!double.IsFinite(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be finite and greater than or equal to 0.");
        }
    }

    protected static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
            await Task.Delay(10, cancellationToken);
    }

    protected static async Task WaitUntilAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        Func<Exception?>? getFailure,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive or infinite.");
        }

        using var timeoutCts = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linkedCts = timeoutCts is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (true)
            {
                bool completed = predicate();
                Exception? failure = getFailure?.Invoke();
                if (failure is not null)
                {
                    throw failure;
                }

                if (completed)
                {
                    return;
                }

                await Task.Delay(10, linkedCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Motion operation did not complete within {timeout}.");
        }
    }
}
