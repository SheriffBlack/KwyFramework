using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;

namespace Kwy.Device.MotionCards.Simulation;

public interface ISimulationMotionControl
{
    void SetPosition(short axis, double position);

    void SetAlarm(short axis, bool active);

    void SetLimit(short axis, bool positive, bool negative);

    void SetHomeFailure(short axis, bool active);
}

public sealed class SimulationMotionCardDevice :
    MotionCardBase,
    IAxisEngineeringUnitProvider,
    ISimulationMotionControl
{
    private readonly SimulationMotionCardConfig config;
    private readonly ConcurrentDictionary<short, AxisState> axes = new();
    private volatile bool connected;

    public SimulationMotionCardDevice(SimulationMotionCardConfig config)
        : base(config.DeviceId, config.DeviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid simulation motion card configuration.", nameof(config));
        }

        for (short axis = 1; axis <= config.AxisCount; axis++)
        {
            axes[axis] = new AxisState(axis);
        }
    }

    public override string DeviceModel => "Simulation";

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connected = true;
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (AxisState state in axes.Values)
        {
            state.Stop();
        }

        connected = false;
        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive() => connected;

    public AxisEngineeringConfig GetAxisEngineeringConfig(short axis)
    {
        ValidateAxis(axis);
        return config.GetAxisEngineeringConfig(axis);
    }

    public override void ServoOn(short axis) => GetState(axis).Update(state => state.ServoEnabled = true);

    public override void ServoOff(short axis)
    {
        AxisState state = GetState(axis);
        state.Stop();
        state.Update(value => value.ServoEnabled = false);
    }

    public override void ClearError(short axis) => GetState(axis).Update(state => state.Alarm = false);

    public override void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5)
        => StartPositionMove(GetState(axis), position, new MotionProfile(velocity, acc, dec));

    public override void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5)
        => MoveAbs(axis, GetPosition(axis) + distance, velocity, acc, dec);

    public override void MoveJog(short axis, double velocity)
    {
        AxisState state = GetState(axis);
        EnsureMovable(state);
        state.Start(async token =>
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                double delta = velocity * config.UpdateInterval.TotalSeconds * config.SimulationSpeedRatio;
                bool limitReached = state.Read(value =>
                    (delta > 0 && (value.PositiveLimit || value.Position + delta > value.PositiveSoftLimit))
                    || (delta < 0 && (value.NegativeLimit || value.Position + delta < value.NegativeSoftLimit)));
                if (limitReached)
                {
                    state.Update(value =>
                    {
                        value.Moving = false;
                        value.Velocity = 0;
                    });
                    return;
                }

                state.Update(value =>
                {
                    value.Position += delta;
                    value.EncoderPosition = value.Position;
                    value.Velocity = velocity;
                    value.Moving = true;
                });
                await Task.Delay(config.UpdateInterval, token).ConfigureAwait(false);
            }
        });
    }

    public override void Stop(short axis) => GetState(axis).Stop();

    public override void Abort(short axis) => GetState(axis).Stop();

    public override void GoHome(short axis)
    {
        AxisState state = GetState(axis);
        EnsureMovable(state);
        state.Update(value => value.HomeState = HomeState.Running);
        state.Start(async token =>
        {
            await MoveToCoreAsync(state, 0, new MotionProfile(20, 100, 100), token).ConfigureAwait(false);
            state.Update(value => value.HomeState = value.HomeFailure ? HomeState.Failed : HomeState.Succeeded);
        });
    }

    public override double GetPosition(short axis) => GetState(axis).Read(state => state.Position);

    public override double GetEncoderPosition(short axis) => GetState(axis).Read(state => state.EncoderPosition);

    public override double GetVelocity(short axis) => GetState(axis).Read(state => state.Velocity);

    public override int GetStatus(short axis) => GetState(axis).Read(CreateRawStatus);

    public override bool IsMoving(short axis) => GetState(axis).Read(state => state.Moving);

    public override bool IsPositiveLimit(short axis) => GetState(axis).Read(state => state.PositiveLimit);

    public override bool IsNegativeLimit(short axis) => GetState(axis).Read(state => state.NegativeLimit);

    public override bool IsAlarm(short axis) => GetState(axis).Read(state => state.Alarm);

    public override HomeStatus GetHomeStatus(short axis)
        => GetState(axis).Read(state => new HomeStatus(axis, state.HomeState, (int)state.HomeState,
            state.HomeState == HomeState.Failed ? $"Simulated homing failure on axis {axis}." : null));

    public override void SetSoftLimit(short axis, double positive, double negative)
    {
        if (positive <= negative)
        {
            throw new ArgumentException("Positive software limit must be greater than negative software limit.");
        }

        GetState(axis).Update(state =>
        {
            state.PositiveSoftLimit = positive;
            state.NegativeSoftLimit = negative;
        });
    }

    public Task WaitForCoordinateSystemCompletedAsync(
        short crdIndex,
        double[] targetPositions,
        double tolerance,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The simulation motion card does not implement coordinate interpolation.");

    public override MotionAxisSnapshot GetAxisSnapshot(short axis)
        => GetState(axis).Read(state => new MotionAxisSnapshot(
            axis,
            state.Position,
            state.EncoderPosition,
            state.Velocity,
            CreateRawStatus(state),
            state.Moving,
            state.Alarm,
            state.PositiveLimit,
            state.NegativeLimit,
            DateTimeOffset.Now,
            state.ServoEnabled,
            state.HomeState));

    public void SetPosition(short axis, double position) => GetState(axis).Update(state =>
    {
        state.Position = position;
        state.EncoderPosition = position;
    });

    public void SetAlarm(short axis, bool active) => GetState(axis).Update(state => state.Alarm = active);

    public void SetLimit(short axis, bool positive, bool negative) => GetState(axis).Update(state =>
    {
        state.PositiveLimit = positive;
        state.NegativeLimit = negative;
    });

    public void SetHomeFailure(short axis, bool active) => GetState(axis).Update(state => state.HomeFailure = active);

    private void StartPositionMove(AxisState state, double target, MotionProfile profile)
    {
        EnsureMovable(state);
        state.Read(value =>
        {
            if (target < value.NegativeSoftLimit || target > value.PositiveSoftLimit)
            {
                throw new InvalidOperationException(
                    $"Axis {value.Axis} target {target} is outside software limits [{value.NegativeSoftLimit}, {value.PositiveSoftLimit}].");
            }

            if (target > value.Position && value.PositiveLimit)
            {
                throw new InvalidOperationException($"Axis {value.Axis} positive limit is active.");
            }

            if (target < value.Position && value.NegativeLimit)
            {
                throw new InvalidOperationException($"Axis {value.Axis} negative limit is active.");
            }

            return true;
        });
        state.Start(token => MoveToCoreAsync(state, target, profile, token));
    }

    private async Task MoveToCoreAsync(AxisState state, double target, MotionProfile profile, CancellationToken token)
    {
        double currentVelocity = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            bool interrupted = state.Read(value =>
                value.Alarm
                || (target > value.Position && value.PositiveLimit)
                || (target < value.Position && value.NegativeLimit));
            if (interrupted)
            {
                state.Update(value =>
                {
                    value.Moving = false;
                    value.Velocity = 0;
                });
                return;
            }

            bool completed = state.Read(value => Math.Abs(target - value.Position) < 1e-9);
            if (completed)
            {
                state.Update(value =>
                {
                    value.Position = target;
                    value.EncoderPosition = target;
                    value.Velocity = 0;
                    value.Moving = false;
                });
                return;
            }

            double intervalSeconds = config.UpdateInterval.TotalSeconds * config.SimulationSpeedRatio;
            currentVelocity = Math.Min(profile.Velocity, currentVelocity + profile.Acceleration * intervalSeconds);
            state.Update(value =>
            {
                double remaining = target - value.Position;
                double step = Math.Sign(remaining) * currentVelocity * intervalSeconds;
                value.Position = Math.Abs(step) >= Math.Abs(remaining) ? target : value.Position + step;
                value.EncoderPosition = value.Position;
                value.Velocity = Math.Sign(remaining) * currentVelocity;
                value.Moving = value.Position != target;
            });

            await Task.Delay(config.UpdateInterval, token).ConfigureAwait(false);
        }
    }

    private void EnsureMovable(AxisState state)
    {
        EnsureConnected();
        state.Read(value =>
        {
            if (!value.ServoEnabled)
            {
                throw new InvalidOperationException($"Axis {value.Axis} servo is disabled.");
            }

            if (value.Alarm)
            {
                throw new InvalidOperationException($"Axis {value.Axis} is in alarm state.");
            }

            return true;
        });
    }

    private AxisState GetState(short axis)
    {
        EnsureConnected();
        ValidateAxis(axis);
        return axes[axis];
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new InvalidOperationException("Simulation motion card is not connected.");
        }
    }

    private void ValidateAxis(short axis)
    {
        if (axis < 1 || axis > config.AxisCount)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), axis, $"Axis must be between 1 and {config.AxisCount}.");
        }
    }

    private static int CreateRawStatus(AxisState state)
        => (state.Alarm ? 0x02 : 0)
            | (state.PositiveLimit ? 0x20 : 0)
            | (state.NegativeLimit ? 0x40 : 0)
            | (state.ServoEnabled ? 0x200 : 0)
            | (state.Moving ? 0x400 : 0);

    private sealed class AxisState
    {
        private readonly object syncRoot = new();
        private CancellationTokenSource? motionCts;

        public AxisState(short axis) => Axis = axis;

        public short Axis { get; }
        public double Position { get; set; }
        public double EncoderPosition { get; set; }
        public double Velocity { get; set; }
        public bool ServoEnabled { get; set; }
        public bool Moving { get; set; }
        public bool Alarm { get; set; }
        public bool PositiveLimit { get; set; }
        public bool NegativeLimit { get; set; }
        public bool HomeFailure { get; set; }
        public HomeState HomeState { get; set; } = HomeState.Idle;
        public double PositiveSoftLimit { get; set; } = double.PositiveInfinity;
        public double NegativeSoftLimit { get; set; } = double.NegativeInfinity;

        public void Start(Func<CancellationToken, Task> operation)
        {
            Stop();
            lock (syncRoot)
            {
                Moving = true;
                motionCts = new CancellationTokenSource();
                CancellationTokenSource owner = motionCts;
                _ = RunAsync(operation, owner);
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (syncRoot)
            {
                cts = motionCts;
                motionCts = null;
                Moving = false;
                Velocity = 0;
            }

            cts?.Cancel();
            cts?.Dispose();
        }

        public void Update(Action<AxisState> update)
        {
            lock (syncRoot)
            {
                update(this);
            }
        }

        public T Read<T>(Func<AxisState, T> read)
        {
            lock (syncRoot)
            {
                return read(this);
            }
        }

        private async Task RunAsync(Func<CancellationToken, Task> operation, CancellationTokenSource owner)
        {
            try
            {
                await operation(owner.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (owner.IsCancellationRequested)
            {
            }
            finally
            {
                lock (syncRoot)
                {
                    if (ReferenceEquals(motionCts, owner))
                    {
                        motionCts = null;
                        Moving = false;
                        Velocity = 0;
                    }
                }

                owner.Dispose();
            }
        }
    }
}
