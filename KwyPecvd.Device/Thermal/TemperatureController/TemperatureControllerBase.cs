namespace KwyPecvd.Device.Thermal
    .TemperatureController;

public abstract class TemperatureControllerBase : HardwareComponentBase, ITemperatureController
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate =
        new(1, 1);

    private double targetTemperature;

    private bool commandedEnabled;

    private DateTimeOffset? outOfToleranceSince;

    private TemperatureControllerSnapshot snapshot;

    protected TemperatureControllerBase(
        TemperatureControllerDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;
        targetTemperature =
            definition.MinimumSetPoint;

        snapshot = new TemperatureControllerSnapshot
        {
            Id = definition.Id,
            SetPoint = targetTemperature,
            Feedback = 0,
            Unit = definition.Unit,
            CommandedEnabled = false,
            IsPowerOn = false,
            IsOffline = true,
            IsFeedbackValid = false,
            IsOutOfTolerance = false,
            DiagnosticCode =
                TemperatureControllerDiagnosticCodes
                    .NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public TemperatureControllerDefinition Definition
    {
        get;
    }

    public TemperatureControllerSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<TemperatureControllerSnapshot>? SnapshotChanged;

    public override sealed async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<CommandResult> SetTargetTemperatureAsync(
        double target,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(target))
        {
            return CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "Temperature target must be finite.");
        }

        if (target < Definition.MinimumSetPoint ||
            target > Definition.MaximumSetPoint)
        {
            return CommandResult.Failure(
                ResultCodes.OutOfRange,
                $"Temperature controller '{Id}' " +
                $"target must be between " +
                $"{Definition.MinimumSetPoint} and " +
                $"{Definition.MaximumSetPoint} " +
                $"{Definition.Unit}.");
        }

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        TemperatureControllerSnapshot changed;

        try
        {
            lock (syncRoot)
            {
                targetTemperature = target;
                outOfToleranceSince = null;

                changed = snapshot with
                {
                    SetPoint = target,
                    IsOutOfTolerance = false,
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
            $"Temperature controller '{Id}' " +
            $"accepted target {target} " +
            $"{Definition.Unit}.");
    }

    public async Task<CommandResult> EnableAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        TemperatureControllerSnapshot changed;

        try
        {
            lock (syncRoot)
            {
                if (snapshot.IsOffline)
                {
                    return CommandResult.Failure(
                        ResultCodes.Offline,
                        $"Temperature controller " +
                        $"'{Id}' is offline.");
                }

                if (!snapshot.IsFeedbackValid)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Temperature controller " +
                        $"'{Id}' feedback is invalid.");
                }

                commandedEnabled = true;
                outOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedEnabled = true,
                    IsOutOfTolerance = false,
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
            $"Temperature controller '{Id}' " +
            "accepted enable command.");
    }

    public async Task<CommandResult> DisableAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        TemperatureControllerSnapshot changed;

        try
        {
            lock (syncRoot)
            {
                commandedEnabled = false;
                outOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedEnabled = false,
                    IsOutOfTolerance = false,
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
            $"Temperature controller '{Id}' " +
            "accepted disable command.");
    }

    public override sealed async Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        TemperatureControllerSnapshot? snapshotToPublish = null;

        try
        {
            double target;
            bool enabled;

            lock (syncRoot)
            {
                target = targetTemperature;
                enabled = commandedEnabled;
            }

            var result =
                await ExchangeAsync(
                        target,
                        enabled,
                        cancellationToken)
                    .ConfigureAwait(false);

            var outOfTolerance =
                CalculateOutOfTolerance(
                    target,
                    result.Feedback,
                    enabled,
                    result.IsOffline,
                    result.IsFeedbackValid,
                    result.Timestamp);

            var diagnosticCode =
                result.IsOffline
                ? TemperatureControllerDiagnosticCodes
                    .Offline
                : result.DiagnosticCode is not null
                    ? result.DiagnosticCode
                    : !result.IsFeedbackValid
                        ? TemperatureControllerDiagnosticCodes
                            .FeedbackBadQuality
                        : outOfTolerance
                            ? TemperatureControllerDiagnosticCodes
                                .OutOfTolerance
                            : null;

            var changed =
                new TemperatureControllerSnapshot
                {
                    Id = Definition.Id,
                    SetPoint = target,
                    Feedback = result.Feedback,
                    Unit = Definition.Unit,
                    CommandedEnabled = enabled,
                    IsPowerOn = result.IsPowerOn,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsOutOfTolerance =
                        outOfTolerance,
                    DiagnosticCode =
                        diagnosticCode,
                    Timestamp = result.Timestamp
                };

            UpdateSnapshot(changed);
            snapshotToPublish = changed;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            TemperatureControllerSnapshot failed;

            lock (syncRoot)
            {
                commandedEnabled = false;
                outOfToleranceSince = null;

                failed = snapshot with
                {
                    CommandedEnabled = false,
                    IsPowerOn = false,
                    IsOffline = true,
                    IsFeedbackValid = false,
                    IsOutOfTolerance = false,
                    DiagnosticCode =
                        TemperatureControllerDiagnosticCodes
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

    public override sealed async Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        TemperatureControllerSnapshot?
            snapshotToPublish = null;

        try
        {
            var safeTarget =
                Definition.MinimumSetPoint;

            var result =
                await ExchangeAsync(
                        safeTarget,
                        enabled: false,
                        cancellationToken)
                    .ConfigureAwait(false);

            targetTemperature = safeTarget;
            commandedEnabled = false;
            outOfToleranceSince = null;

            var changed =
                new TemperatureControllerSnapshot
                {
                    Id = Definition.Id,
                    SetPoint = safeTarget,
                    Feedback = result.Feedback,
                    Unit = Definition.Unit,
                    CommandedEnabled = false,
                    IsPowerOn = result.IsPowerOn,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsOutOfTolerance = false,
                    DiagnosticCode =
                        result.DiagnosticCode,
                    Timestamp = result.Timestamp
                };

            UpdateSnapshot(changed);
            snapshotToPublish = changed;
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

    private bool CalculateOutOfTolerance(
        double target,
        double feedback,
        bool enabled,
        bool offline,
        bool feedbackValid,
        DateTimeOffset now)
    {
        var exceedsTolerance =
            enabled &&
            !offline &&
            feedbackValid &&
            Math.Abs(feedback - target) >
                Definition.Tolerance;

        if (!exceedsTolerance)
        {
            outOfToleranceSince = null;
            return false;
        }

        outOfToleranceSince ??= now;

        return now - outOfToleranceSince.Value >=
            Definition.ToleranceDelay;
    }

    private void UpdateSnapshot(
    TemperatureControllerSnapshot current)
    {
        lock (syncRoot)
        {
            snapshot = current;
        }
    }

    private void PublishSnapshotChanged(
        TemperatureControllerSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<
                TemperatureControllerSnapshot>>()
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

    protected abstract ValueTask<
        TemperatureControllerCycleResult>
        ExchangeAsync(
            double target,
            bool enabled,
            CancellationToken cancellationToken);

    protected readonly record struct
        TemperatureControllerCycleResult(
            double Feedback,
            bool IsPowerOn,
            bool IsOffline,
            bool IsFeedbackValid,
            DateTimeOffset Timestamp,
            string? DiagnosticCode = null);
}
