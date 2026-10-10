namespace KwyPecvd.Device.Mfc;

/// <summary>统一实现MFC命令、线性斜坡、容差延时、状态快照和并发控制。</summary>
public abstract class MfcBase : HardwareComponentBase, IMfc
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private MfcSnapshot snapshot;
    private double rampStart;   // 斜坡起点
    private double rampTarget;  // 斜坡目标
    private TimeSpan rampDuration;  // 计划持续时间
    private DateTimeOffset rampStartedAt;   // 开始时刻
    private DateTimeOffset? outOfToleranceSince;

    protected MfcBase(MfcDefinition definition, bool initiallyOffline)
        : base(definition?.Id ?? throw new ArgumentNullException(nameof(definition)))
    {
        definition.Validate();
        Definition = definition;
        var now = DateTimeOffset.UtcNow;
        rampStartedAt = now;
        snapshot = new MfcSnapshot
        {
            Id = definition.Id,
            SetPoint = 0,
            Feedback = 0,
            FullScale = definition.FullScale,
            Unit = definition.Unit,
            IsRamping = false,
            IsOffline = initiallyOffline,
            IsOutOfTolerance = false,
            Timestamp = now,
            IsFeedbackValid = false,
            DiagnosticCode = MfcDiagnosticCodes.NotSampled,
        };
    }

    public MfcDefinition Definition { get; }

    public MfcSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
                return snapshot;
        }
    }

    public event EventHandler<MfcSnapshot>? SnapshotChanged;

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
                snapshot = snapshot with
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
        MfcSnapshot newSnapshot;
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
                newSnapshot = snapshot with { SetPoint = holdValue, IsRamping = false, Timestamp = now };
                snapshot = newSnapshot;
            }
        }
        finally
        {
            operationGate.Release();
        }

        PublishSnapshotChanged(newSnapshot);
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

            MfcCycleResult result =
                await ExchangeAsync(
                    nextSetPoint,
                    cancellationToken)
                .ConfigureAwait(false);

            MfcSnapshot newSnapshot;

            lock (syncRoot)
            {
                var isRamping =
                    Math.Abs(nextSetPoint - rampTarget) >
                    0.001;

                var isOutOfTolerance =
                    CalculateOutOfTolerance(
                        nextSetPoint,
                        result.Feedback,
                        isRamping,
                        result.IsOffline,
                        result.IsFeedbackValid,
                        result.Timestamp);

                newSnapshot = snapshot with
                {
                    SetPoint = nextSetPoint,
                    Feedback = result.Feedback,
                    IsRamping = isRamping,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsOutOfTolerance =
                        isOutOfTolerance,
                    DiagnosticCode =
                        result.DiagnosticCode,
                    Timestamp = result.Timestamp
                };

                snapshot = newSnapshot;
            }

            PublishSnapshotChanged(newSnapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            MfcSnapshot offlineSnapshot;
            lock (syncRoot)
            {
                outOfToleranceSince = null;
                offlineSnapshot = snapshot with
                {
                    IsOffline = true,
                    IsFeedbackValid = false,
                    IsOutOfTolerance = false,
                    DiagnosticCode = MfcDiagnosticCodes.ExchangeFailed,
                    Timestamp = DateTimeOffset.UtcNow
                };
                snapshot = offlineSnapshot;
            }
            PublishSnapshotChanged(offlineSnapshot);
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
        MfcSnapshot newSnapshot;
        try
        {
            lock (syncRoot)
            {
                outOfToleranceSince = null;
                newSnapshot = snapshot with
                {
                    IsOutOfTolerance = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
                snapshot = newSnapshot;
            }
        }
        finally
        {
            operationGate.Release();
        }
        PublishSnapshotChanged(newSnapshot);
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 计算斜坡
    /// </summary>
    /// <remarks>
    /// 进度 = 已经过的时间 ÷ 总斜坡时间
    /// 当前设定值 = 起点 + (目标 - 起点) × 进度
    /// </remarks>
    /// <returns></returns>
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
        bool isFeedbackValid,
        DateTimeOffset now)
    {
        var exceedsTolerance =
            !isOffline &&
            isFeedbackValid &&
            !isRamping &&
            setPoint > 0.001 &&
            Math.Abs(feedback - setPoint) >
                Definition.Tolerance;
        if (!exceedsTolerance)
        {
            outOfToleranceSince = null;
            return false;
        }

        outOfToleranceSince ??= now;
        return now - outOfToleranceSince.Value >= Definition.ToleranceDelay;
    }

    private void PublishSnapshotChanged(MfcSnapshot newSnapshot)
    {
        var handlers = SnapshotChanged?.GetInvocationList().Cast<EventHandler<MfcSnapshot>>().ToArray();
        if (handlers is null) return;
        foreach (var handler in handlers)
        {
            try { handler(this, newSnapshot); }
            catch { /* 观察者异常不能中断硬件周期；后续接入日志。 */ }
        }
    }

    protected readonly record struct MfcCycleResult(
        double Feedback,
        bool IsOffline,
        bool IsFeedbackValid,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}
