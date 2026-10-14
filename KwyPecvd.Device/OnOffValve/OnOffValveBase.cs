namespace KwyPecvd.Device.OnOffValve;

public abstract class OnOffValveBase : HardwareComponentBase, IOnOffValve
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);

    private OnOffValvePosition commandedPosition;

    private DateTimeOffset? motionStartedAt;

    private OnOffValveSnapshot snapshot;

    protected OnOffValveBase(
        OnOffValveDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;
        commandedPosition = definition.SafePosition;

        snapshot = new OnOffValveSnapshot
        {
            Id = definition.Id,
            CommandedPosition =
                definition.SafePosition,
            State = OnOffValveState.Unknown,
            IsOffline = true,
            IsFeedbackValid = false,
            IsMotionTimedOut = false,
            DiagnosticCode =
                OnOffValveDiagnosticCodes.NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public OnOffValveDefinition Definition { get; }

    public OnOffValveSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<OnOffValveSnapshot>? SnapshotChanged;

    public sealed override async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<CommandResult> OpenAsync(
        CancellationToken cancellationToken = default) =>
        RequestPositionAsync(
            OnOffValvePosition.Open,
            cancellationToken);

    public Task<CommandResult> CloseAsync(
        CancellationToken cancellationToken = default) =>
        RequestPositionAsync(
            OnOffValvePosition.Closed,
            cancellationToken);

    private async Task<CommandResult> RequestPositionAsync(
        OnOffValvePosition target,
        CancellationToken cancellationToken)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        OnOffValveSnapshot? changed;

        try
        {
            lock (syncRoot)
            {
                // 非安全方向的动作必须具备有效反馈。
                if (target != Definition.SafePosition)
                {
                    if (snapshot.IsOffline)
                    {
                        return CommandResult.Failure(
                            ResultCodes.Offline,
                            $"Valve '{Id}' is offline.");
                    }

                    if (!snapshot.IsFeedbackValid)
                    {
                        return CommandResult.Failure(
                            ResultCodes.NotReady,
                            $"Valve '{Id}' feedback " +
                            "is invalid.");
                    }
                }

                // 重复命令保持幂等，不重新计算超时。
                if (commandedPosition == target)
                {
                    return CommandResult.Success(
                        $"Valve '{Id}' already accepted " +
                        $"{target}.");
                }

                commandedPosition = target;
                motionStartedAt = DateTimeOffset.UtcNow;

                changed = snapshot with
                {
                    CommandedPosition = target,
                    State = target ==
                        OnOffValvePosition.Open
                            ? OnOffValveState.Opening
                            : OnOffValveState.Closing,
                    IsMotionTimedOut = false,
                    DiagnosticCode = null,
                    Timestamp = DateTimeOffset.UtcNow
                };

                snapshot = changed;
            }
        }
        finally
        {
            operationGate.Release();
        }

        PublishSnapshotChanged(changed);

        return CommandResult.Success(
            $"Valve '{Id}' accepted {target}.");
    }

    public sealed override async Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        OnOffValveSnapshot? snapshotToPublish = null;

        try
        {
            OnOffValvePosition requested;

            lock (syncRoot)
            {
                requested = commandedPosition;
            }

            var result =
                await ExchangeAsync(
                        requested,
                        cancellationToken)
                    .ConfigureAwait(false);

            snapshotToPublish =
                UpdateSnapshot(requested, result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            OnOffValveSnapshot failed;

            lock (syncRoot)
            {
                commandedPosition =
                    Definition.SafePosition;

                motionStartedAt = null;

                failed = snapshot with
                {
                    CommandedPosition =
                        Definition.SafePosition,
                    State = OnOffValveState.Unknown,
                    IsOffline = true,
                    IsFeedbackValid = false,
                    IsMotionTimedOut = false,
                    DiagnosticCode =
                        OnOffValveDiagnosticCodes
                            .ExchangeFailed,
                    Timestamp = DateTimeOffset.UtcNow
                };

                snapshot = failed;
            }

            snapshotToPublish = failed;
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

    public sealed override async Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        OnOffValveSnapshot? snapshotToPublish = null;

        try
        {
            var safePosition =
                Definition.SafePosition;

            lock (syncRoot)
            {
                commandedPosition = safePosition;
                motionStartedAt = DateTimeOffset.UtcNow;
            }

            var result =
                await ExchangeAsync(
                        safePosition,
                        cancellationToken)
                    .ConfigureAwait(false);

            snapshotToPublish = UpdateSnapshot(
                safePosition,
                result);
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

    protected virtual Task ShutdownCoreAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private OnOffValveSnapshot UpdateSnapshot(
        OnOffValvePosition requested,
        OnOffValveCycleResult result)
    {
        OnOffValveState state;
        bool timedOut;

        lock (syncRoot)
        {
            if (result.IsFeedbackConflict)
            {
                state = OnOffValveState.Fault;
                timedOut = false;
            }
            else if (result.IsOffline ||
                     !result.IsFeedbackValid)
            {
                state = OnOffValveState.Unknown;
                timedOut = false;
            }
            else if (result.ActualPosition == requested)
            {
                state = requested ==
                    OnOffValvePosition.Open
                        ? OnOffValveState.Open
                        : OnOffValveState.Closed;

                timedOut = false;
                motionStartedAt = null;
            }
            else
            {
                motionStartedAt ??= result.Timestamp;

                timedOut =
                    result.Timestamp -
                    motionStartedAt.Value >=
                    Definition.MotionTimeout;

                state = timedOut
                    ? OnOffValveState.Fault
                    : requested ==
                        OnOffValvePosition.Open
                            ? OnOffValveState.Opening
                            : OnOffValveState.Closing;
            }

            snapshot = new OnOffValveSnapshot
            {
                Id = Definition.Id,
                CommandedPosition = requested,
                State = state,
                IsOffline = result.IsOffline,
                IsFeedbackValid =
                    result.IsFeedbackValid,
                IsMotionTimedOut = timedOut,
                DiagnosticCode = GetDiagnosticCode(
                    result,
                    timedOut),
                Timestamp = result.Timestamp
            };
        }

        return snapshot;
    }

    private static string? GetDiagnosticCode(
        OnOffValveCycleResult result,
        bool timedOut)
    {
        if (result.IsOffline)
        {
            return OnOffValveDiagnosticCodes.Offline;
        }

        if (result.IsFeedbackConflict)
        {
            return OnOffValveDiagnosticCodes
                .FeedbackConflict;
        }

        if (!result.IsFeedbackValid)
        {
            return OnOffValveDiagnosticCodes
                .FeedbackBadQuality;
        }

        if (timedOut)
        {
            return OnOffValveDiagnosticCodes
                .MotionTimeout;
        }

        return result.DiagnosticCode;
    }

    private void PublishSnapshotChanged(
        OnOffValveSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<OnOffValveSnapshot>>()
            .ToArray();

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(this, current);
            }
            catch
            {
                // 观察者异常不能中断硬件周期。
            }
        }
    }
    protected abstract ValueTask<OnOffValveCycleResult> ExchangeAsync(
        OnOffValvePosition commandedPosition,
        CancellationToken cancellationToken);

    protected readonly record struct OnOffValveCycleResult(
        OnOffValvePosition? ActualPosition,
        bool IsOffline,
        bool IsFeedbackValid,
        bool IsFeedbackConflict,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}
