namespace KwyPecvd.Device.Mfc;

/// <summary>统一实现MFC命令、线性斜坡、容差延时、状态快照和并发控制。</summary>
public abstract class MfcBase : HardwareComponentBase, IMfc
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private MfcState state;
    private double rampStart;
    private double rampTarget;
    private TimeSpan rampDuration;
    private DateTimeOffset rampStartedAt;
    private DateTimeOffset? outOfToleranceSince;

    protected MfcBase(MfcDefinition definition, bool initiallyOffline)
        : base(definition?.Id ?? throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();
        Definition = definition;
        var now = DateTimeOffset.UtcNow;
        rampStartedAt = now;
        state = new MfcState
        {
            Id = definition.Id,
            SetPoint = 0,
            Feedback = 0,
            FullScale = definition.FullScale,
            Unit = definition.Unit,
            IsRamping = false,
            IsOffline = initiallyOffline,
            IsOutOfTolerance = false,
            Timestamp = now
        };
    }

    public MfcDefinition Definition { get; }

    public MfcState State
    {
        get
        {
            lock (syncRoot)
                return state;
        }
    }

    public event EventHandler<MfcState>? StateChanged;

    public sealed override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Definition.Validate();
        await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<CommandResult> SetFlowAsync(
        double target,
        TimeSpan rampDuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!double.IsFinite(target))
            return CommandResult.Failure(ResultCodes.InvalidArgument, "MFC target must be a finite number.");
        if (target < 0 || target > Definition.FullScale)
            return CommandResult.Failure(ResultCodes.OutOfRange,
                $"MFC {Id} target must be between 0 and {Definition.FullScale} {Definition.Unit}.");
        if (rampDuration < TimeSpan.Zero)
            return CommandResult.Failure(ResultCodes.InvalidArgument, "Ramp duration cannot be negative.");

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (syncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                var currentSetPoint = CalculateSetPoint(now);
                rampStart = currentSetPoint;
                rampTarget = target;
                this.rampDuration = rampDuration;
                rampStartedAt = now;
                outOfToleranceSince = null;
                state = state with
                {
                    SetPoint = currentSetPoint,
                    IsRamping = rampDuration > TimeSpan.Zero && Math.Abs(target - currentSetPoint) > 0.001,
                    IsOutOfTolerance = false,
                    Timestamp = now
                };
            }
        }
        finally
        {
            operationGate.Release();
        }

        return CommandResult.Success($"MFC {Id} accepted target {target} {Definition.Unit}.");
    }

    public Task<CommandResult> StopAsync(CancellationToken cancellationToken = default) =>
        SetFlowAsync(0, TimeSpan.Zero, cancellationToken);

    public async Task<CommandResult> HoldAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MfcState newState;
        double holdValue;

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (syncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                holdValue = CalculateSetPoint(now);
                rampStart = holdValue;
                rampTarget = holdValue;
                rampDuration = TimeSpan.Zero;
                rampStartedAt = now;
                newState = state with { SetPoint = holdValue, IsRamping = false, Timestamp = now };
                state = newState;
            }
        }
        finally
        {
            operationGate.Release();
        }

        PublishStateChanged(newState);
        return CommandResult.Success($"MFC {Id} is holding at {holdValue:F3} {Definition.Unit}.");
    }

    public sealed override async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            double nextSetPoint;
            lock (syncRoot)
                nextSetPoint = CalculateSetPoint(DateTimeOffset.UtcNow);

            MfcCycleResult result = await ExchangeAsync(nextSetPoint, cancellationToken).ConfigureAwait(false);
            MfcState newState;
            lock (syncRoot)
            {
                var now = result.Timestamp;
                var isRamping = Math.Abs(nextSetPoint - rampTarget) > 0.001;
                var isOutOfTolerance = CalculateOutOfTolerance(
                    nextSetPoint, result.Feedback, isRamping, result.IsOffline, now);
                newState = state with
                {
                    SetPoint = nextSetPoint,
                    Feedback = result.Feedback,
                    IsRamping = isRamping,
                    IsOffline = result.IsOffline,
                    IsOutOfTolerance = isOutOfTolerance,
                    Timestamp = now
                };
                state = newState;
            }

            PublishStateChanged(newState);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            MfcState offlineState;
            lock (syncRoot)
            {
                outOfToleranceSince = null;
                offlineState = state with
                {
                    IsOffline = true,
                    IsOutOfTolerance = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
                state = offlineState;
            }
            PublishStateChanged(offlineState);
            throw;
        }
        finally
        {
            operationGate.Release();
        }
    }

    protected abstract ValueTask<MfcCycleResult> ExchangeAsync(
        double setPoint,
        CancellationToken cancellationToken);

    public override async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        MfcState newState;
        try
        {
            lock (syncRoot)
            {
                outOfToleranceSince = null;
                newState = state with
                {
                    IsOutOfTolerance = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
                state = newState;
            }
        }
        finally
        {
            operationGate.Release();
        }
        PublishStateChanged(newState);
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private double CalculateSetPoint(DateTimeOffset now)
    {
        if (rampDuration <= TimeSpan.Zero) return rampTarget;
        var elapsed = now - rampStartedAt;
        if (elapsed >= rampDuration) return rampTarget;
        var progress = Math.Clamp(elapsed.TotalMilliseconds / rampDuration.TotalMilliseconds, 0, 1);
        return rampStart + (rampTarget - rampStart) * progress;
    }

    private bool CalculateOutOfTolerance(
        double setPoint,
        double feedback,
        bool isRamping,
        bool isOffline,
        DateTimeOffset now)
    {
        var exceedsTolerance = !isOffline && !isRamping && setPoint > 0.001 &&
                               Math.Abs(feedback - setPoint) > Definition.Tolerance;
        if (!exceedsTolerance)
        {
            outOfToleranceSince = null;
            return false;
        }

        outOfToleranceSince ??= now;
        return now - outOfToleranceSince.Value >= Definition.ToleranceDelay;
    }

    private void PublishStateChanged(MfcState newState)
    {
        var handlers = StateChanged?.GetInvocationList().Cast<EventHandler<MfcState>>().ToArray();
        if (handlers is null) return;
        foreach (var handler in handlers)
        {
            try { handler(this, newState); }
            catch { /* 观察者异常不能中断硬件周期；后续接入日志。 */ }
        }
    }

    protected readonly record struct MfcCycleResult(
        double Feedback,
        bool IsOffline,
        DateTimeOffset Timestamp);
}
