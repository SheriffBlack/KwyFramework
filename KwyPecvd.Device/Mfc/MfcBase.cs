using KwyPecvd.Device.Mfc.Ramp;

namespace KwyPecvd.Device.Mfc;

/// <summary>统一实现 MFC 命令、容差延时、状态快照和并发控制。</summary>
public abstract class MfcBase : HardwareComponentBase, IMfc, IResettableHardwareComponent
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly IMfcRampProfile rampProfile;
    private MfcSnapshot snapshot;
    private DateTimeOffset? outOfToleranceSince;

    protected MfcBase(
        MfcDefinition definition,
        IMfcRampProfile rampProfile)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;
        this.rampProfile = rampProfile ?? throw new ArgumentNullException(nameof(rampProfile));

        var now = DateTimeOffset.UtcNow;

        snapshot = new MfcSnapshot
        {
            Id = definition.Id,
            TargetSetPoint = 0,
            Feedback = 0,
            FullScale = definition.FullScale,
            Unit = definition.Unit,
            IsRamping = false,
            IsOffline = true,
            IsFeedbackValid = false,
            IsOutOfTolerance = false,
            DiagnosticCode =
                MfcDiagnosticCodes.NotSampled,
            Timestamp = now
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

    public override sealed async Task InitializeAsync(CancellationToken cancellationToken = default)
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
        MfcSnapshot changed;
        try
        {
            var now = DateTimeOffset.UtcNow;
            MfcRampStep step = rampProfile.SetTarget(target, rampDuration, now);
            await WriteIfRequiredAsync(step, cancellationToken).ConfigureAwait(false);

            lock (syncRoot)
            {
                outOfToleranceSince = null;

                changed = snapshot with
                {
                    TargetSetPoint = step.TargetSetPoint,
                    IsRamping = step.IsRamping,
                    IsOutOfTolerance = false,
                    Timestamp = now
                };

                snapshot = changed;
            }
        }
        finally
        {
            operationGate.Release();
        }

        PublishSnapshotChanged(changed);

        return CommandResult.Success($"MFC {Id} accepted target {target} {Definition.Unit}.");
    }

    public Task<CommandResult> StopAsync(CancellationToken cancellationToken = default) => SetFlowAsync(0, TimeSpan.Zero, cancellationToken);

    public async Task<CommandResult> HoldAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        MfcSnapshot newSnapshot;
        try
        {
            var now = DateTimeOffset.UtcNow;
            double feedback;
            lock (syncRoot)
                feedback = snapshot.Feedback;

            MfcRampStep step = rampProfile.Hold(feedback, now);
            await WriteIfRequiredAsync(step, cancellationToken).ConfigureAwait(false);

            lock (syncRoot)
            {
                newSnapshot = snapshot with
                {
                    TargetSetPoint = step.TargetSetPoint,
                    IsRamping = false,
                    Timestamp = now
                };
                snapshot = newSnapshot;
            }
        }
        finally
        {
            operationGate.Release();
        }

        PublishSnapshotChanged(newSnapshot);
        return CommandResult.Success(
            $"MFC {Id} is holding at {newSnapshot.TargetSetPoint:F3} {Definition.Unit}.");
    }

    public override sealed async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        MfcSnapshot? snapshotToPublish = null;

        try
        {
            MfcRampStep step = rampProfile.Evaluate(DateTimeOffset.UtcNow);
            await WriteIfRequiredAsync(step, cancellationToken).ConfigureAwait(false);

            MfcCycleResult result = await ReadFeedbackAsync(cancellationToken).ConfigureAwait(false);

            MfcSnapshot newSnapshot;

            lock (syncRoot)
            {
                var isOutOfTolerance =
                    CalculateOutOfTolerance(
                        step.TargetSetPoint,
                        result.Feedback,
                        step.IsRamping,
                        result.IsOffline,
                        result.IsFeedbackValid,
                        result.Timestamp);

                // with生成新Snapshot
                newSnapshot = snapshot with
                {
                    TargetSetPoint = step.TargetSetPoint,
                    Feedback = result.Feedback,
                    IsRamping = step.IsRamping,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsOutOfTolerance =
                        isOutOfTolerance,
                    DiagnosticCode =
                        GetDiagnosticCode(
                            result,
                            isOutOfTolerance),
                    Timestamp = result.Timestamp
                };

                snapshot = newSnapshot;
            }

            snapshotToPublish = newSnapshot;
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
            snapshotToPublish = offlineSnapshot;
            throw;
        }
        finally
        {
            operationGate.Release();

            if (snapshotToPublish is not null)
            {
                PublishSnapshotChanged(
                    snapshotToPublish);
            }
        }
    }

    /// <summary> 将当前斜坡决策写入硬件或输出映像。</summary>
    protected abstract ValueTask WriteSetPointAsync(
        double setPoint,
        TimeSpan rampDuration,
        CancellationToken cancellationToken);

    /// <summary>读取硬件反馈或总线输入映像。</summary>
    protected abstract ValueTask<MfcCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken);

    public async Task<CommandResult> ResetAsync(CancellationToken cancellationToken = default)
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

        return CommandResult.Success(
            $"MFC '{Id}' software diagnostic state was reset.");
    }

    public override sealed async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        MfcSnapshot? snapshotToPublish = null;

        try
        {
            var now = DateTimeOffset.UtcNow;
            MfcRampStep step = rampProfile.SetTarget(0, TimeSpan.Zero, now);
            await WriteIfRequiredAsync(step, cancellationToken).ConfigureAwait(false);

            step = rampProfile.Evaluate(now);
            await WriteIfRequiredAsync(step, cancellationToken).ConfigureAwait(false);

            var result = await ReadFeedbackAsync(cancellationToken).ConfigureAwait(false);

            MfcSnapshot newSnapshot;

            lock (syncRoot)
            {
                outOfToleranceSince = null;

                newSnapshot = snapshot with
                {
                    TargetSetPoint = 0,
                    Feedback = result.Feedback,
                    IsRamping = false,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsOutOfTolerance = false,
                    DiagnosticCode =
                        GetDiagnosticCode(
                            result,
                            outOfTolerance: false),
                    Timestamp = now
                };

                snapshot = newSnapshot;
            }

            snapshotToPublish = newSnapshot;
        }
        finally
        {
            try
            {
                await ShutdownCoreAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                operationGate.Release();

                if (snapshotToPublish is not null)
                {
                    PublishSnapshotChanged(
                        snapshotToPublish);
                }
            }
        }
    }

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private static string? GetDiagnosticCode(
        MfcCycleResult result,
        bool outOfTolerance)
    {
        if (result.IsOffline)
        {
            return MfcDiagnosticCodes.Offline;
        }

        if (!result.IsFeedbackValid)
        {
            return MfcDiagnosticCodes
                .FeedbackBadQuality;
        }

        if (outOfTolerance)
        {
            return MfcDiagnosticCodes
                .OutOfTolerance;
        }

        return result.DiagnosticCode;
    }

    private async ValueTask WriteIfRequiredAsync(
        MfcRampStep step,
        CancellationToken cancellationToken)
    {
        if (!step.RequiresWrite)
            return;

        await WriteSetPointAsync(
                step.CommandSetPoint,
                step.RampDuration,
                cancellationToken)
            .ConfigureAwait(false);

        rampProfile.ConfirmWritten(step.CommandSetPoint);
    }

    /// <summary>判断 MFC 的实际流量是否长时间没有跟上目标流量。
    /// 类似运动控制卡是否到达位置，在额度范围内就算到位
    /// </summary>
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
