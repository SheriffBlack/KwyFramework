namespace KwyPecvd.Device.VacuumPump;

public abstract class VacuumPumpBase : HardwareComponentBase, IVacuumPump
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);

    /// <summary>软件当前要求真空泵启动还是停止。</summary>
    private bool commandedOn;

    private VacuumPumpSnapshot snapshot;

    protected VacuumPumpBase(VacuumPumpDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        snapshot = new VacuumPumpSnapshot
        {
            Id = definition.Id,
            CommandedOn = false,
            IsRunning = false,
            HasWarning = false,
            HasAlarm = false,
            IsOverloaded = false,
            IsN2Ready = false,
            IsExhaustReady = false,
            IsOffline = true,
            IsFeedbackValid = false,
            DiagnosticCode =
                VacuumPumpDiagnosticCodes.NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public VacuumPumpDefinition Definition { get; }

    public VacuumPumpSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<VacuumPumpSnapshot>? SnapshotChanged;

    public sealed override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<CommandResult> StartAsync(CancellationToken cancellationToken = default)
    {
        VacuumPumpSnapshot changed;

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            lock (syncRoot)
            {
                if (snapshot.IsOffline)
                {
                    return CommandResult.Failure(
                        ResultCodes.Offline,
                        $"Vacuum pump '{Id}' " +
                        "is offline.");
                }

                if (!snapshot.IsFeedbackValid)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{Id}' " +
                        "feedback is invalid.");
                }

                if (snapshot.HasAlarm)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{Id}' " +
                        "has an alarm.");
                }

                if (snapshot.IsOverloaded)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{Id}' " +
                        "is overloaded.");
                }

                if (!snapshot.IsN2Ready)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{Id}' " +
                        "N2 supply is not ready.");
                }

                if (!snapshot.IsExhaustReady)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{Id}' " +
                        "exhaust pressure is not ready.");
                }

            }

            await WriteRunningAsync(
                    running: true,
                    cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                commandedOn = true;

                changed = snapshot with
                {
                    CommandedOn = true,
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
            $"Vacuum pump '{Id}' accepted " +
            "the start command.");
    }

    public async Task<CommandResult> StopAsync(CancellationToken cancellationToken = default)
    {
        VacuumPumpSnapshot changed;

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await WriteRunningAsync(
                    running: false,
                    cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                commandedOn = false;

                changed = snapshot with
                {
                    CommandedOn = false,
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
            $"Vacuum pump '{Id}' accepted " +
            "the stop command.");
    }

    public sealed override async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        VacuumPumpSnapshot? snapshotToPublish = null;

        try
        {
            bool requestedOn;

            lock (syncRoot)
            {
                requestedOn = commandedOn;
            }

            var result =
                await ReadFeedbackAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            // 严重硬件故障出现后，立即撤销硬件启动命令。
            var effectiveCommand =
                requestedOn &&
                !result.IsOffline &&
                result.IsFeedbackValid &&
                !result.HasAlarm &&
                !result.IsOverloaded;

            if (requestedOn && !effectiveCommand)
            {
                await WriteRunningAsync(
                        running: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            lock (syncRoot)
            {
                commandedOn = effectiveCommand;
            }

            snapshotToPublish = UpdateSnapshot(
                result,
                effectiveCommand);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            try
            {
                await WriteRunningAsync(
                        running: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // 保留原始周期异常；快照仍进入离线安全状态。
            }

            VacuumPumpSnapshot failed;

            lock (syncRoot)
            {
                commandedOn = false;

                failed = snapshot with
                {
                    CommandedOn = false,
                    IsOffline = true,
                    IsFeedbackValid = false,
                    DiagnosticCode =
                        VacuumPumpDiagnosticCodes
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

    public sealed override async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        VacuumPumpSnapshot? snapshotToPublish = null;

        try
        {
            await WriteRunningAsync(
                    running: false,
                    cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                commandedOn = false;
            }

            var result =
                await ReadFeedbackAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            snapshotToPublish = UpdateSnapshot(
                result,
                effectiveCommand: false);
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

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>立即向硬件写入泵的运行命令。</summary>
    protected abstract ValueTask WriteRunningAsync(
        bool running,
        CancellationToken cancellationToken);

    /// <summary>周期读取泵反馈，不重复写入运行命令。</summary>
    protected abstract ValueTask<VacuumPumpCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken);

    private VacuumPumpSnapshot UpdateSnapshot(
        VacuumPumpCycleResult result,
        bool effectiveCommand)
    {
        var diagnosticCode = GetDiagnosticCode(result);

        VacuumPumpSnapshot current;

        lock (syncRoot)
        {
            current = new VacuumPumpSnapshot
            {
                Id = Definition.Id,
                CommandedOn = effectiveCommand,
                IsRunning = result.IsRunning,
                HasWarning = result.HasWarning,
                HasAlarm = result.HasAlarm,
                IsOverloaded =
                    result.IsOverloaded,
                IsN2Ready = result.IsN2Ready,
                IsExhaustReady =
                    result.IsExhaustReady,
                IsOffline = result.IsOffline,
                IsFeedbackValid =
                    result.IsFeedbackValid,
                DiagnosticCode = diagnosticCode,
                Timestamp = result.Timestamp
            };

            snapshot = current;
        }

        return current;
    }

    private static string? GetDiagnosticCode(VacuumPumpCycleResult result)
    {
        if (result.IsOffline)
        {
            return VacuumPumpDiagnosticCodes
                .Offline;
        }

        if (!result.IsFeedbackValid)
        {
            return VacuumPumpDiagnosticCodes
                .FeedbackBadQuality;
        }

        if (result.IsOverloaded)
        {
            return VacuumPumpDiagnosticCodes
                .Overload;
        }

        if (result.HasAlarm)
        {
            return VacuumPumpDiagnosticCodes
                .Alarm;
        }

        if (!result.IsN2Ready)
        {
            return VacuumPumpDiagnosticCodes
                .N2NotReady;
        }

        if (!result.IsExhaustReady)
        {
            return VacuumPumpDiagnosticCodes
                .ExhaustNotReady;
        }

        if (result.HasWarning)
        {
            return VacuumPumpDiagnosticCodes
                .Warning;
        }

        return result.DiagnosticCode;
    }

    private void PublishSnapshotChanged(VacuumPumpSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<VacuumPumpSnapshot>>()
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

    protected readonly record struct VacuumPumpCycleResult(
        bool IsRunning,
        bool HasWarning,
        bool HasAlarm,
        bool IsOverloaded,
        bool IsN2Ready,
        bool IsExhaustReady,
        bool IsOffline,
        bool IsFeedbackValid,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}
