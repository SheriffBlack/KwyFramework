namespace KwyPecvd.Device.RfMatcher;

public abstract class RfMatcherBase : HardwareComponentBase, IRfMatcher
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);

    private RfMatcherMode commandedMode = RfMatcherMode.Unknown;

    private double tuneSetPoint;

    private double loadSetPoint;

    private DateTimeOffset? positionOutOfToleranceSince;

    private RfMatcherSnapshot snapshot;

    protected RfMatcherBase(RfMatcherDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        tuneSetPoint =
            definition.MinimumPosition;

        loadSetPoint =
            definition.MinimumPosition;

        snapshot = new RfMatcherSnapshot
        {
            Id = definition.Id,
            CommandedMode =
                RfMatcherMode.Unknown,
            ActualMode =
                RfMatcherMode.Unknown,
            TuneSetPoint = tuneSetPoint,
            TunePosition =
                definition.MinimumPosition,
            LoadSetPoint = loadSetPoint,
            LoadPosition =
                definition.MinimumPosition,
            PositionUnit =
                definition.PositionUnit,
            IsTuning = false,
            IsMatched = false,
            IsOffline = true,
            IsFeedbackValid = false,
            IsFaulted = false,
            IsPositionOutOfTolerance = false,
            DiagnosticCode =
                RfMatcherDiagnosticCodes
                    .NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public RfMatcherDefinition Definition { get; }

    public RfMatcherSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<RfMatcherSnapshot>? SnapshotChanged;

    public override sealed async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<CommandResult> SetModeAsync(
        RfMatcherMode mode,
        CancellationToken cancellationToken = default)
    {
        if (mode is not (
            RfMatcherMode.Auto or
            RfMatcherMode.Manual))
        {
            return CommandResult.Failure(
                ResultCodes.InvalidArgument,
                $"RF matcher '{Id}' mode " +
                $"'{mode}' is not supported.");
        }

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfMatcherSnapshot changed;

        try
        {
            lock (syncRoot)
            {
                commandedMode = mode;
                positionOutOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedMode = mode,
                    IsPositionOutOfTolerance =
                        false,
                    Timestamp =
                        DateTimeOffset.UtcNow
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
            $"RF matcher '{Id}' accepted " +
            $"mode '{mode}'.");
    }

    public async Task<CommandResult> SetManualPositionAsync(
            double tunePosition,
            double loadPosition,
            CancellationToken cancellationToken =
                default)
    {
        if (!IsPositionValid(tunePosition) ||
            !IsPositionValid(loadPosition))
        {
            return CommandResult.Failure(
                ResultCodes.OutOfRange,
                $"RF matcher '{Id}' positions " +
                $"must be between " +
                $"{Definition.MinimumPosition} and " +
                $"{Definition.MaximumPosition} " +
                $"{Definition.PositionUnit}.");
        }

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfMatcherSnapshot changed;

        try
        {
            lock (syncRoot)
            {
                if (commandedMode !=
                    RfMatcherMode.Manual)
                {
                    return CommandResult.Failure(
                        ResultCodes.InvalidState,
                        $"RF matcher '{Id}' must be " +
                        "in manual mode before setting " +
                        "positions.");
                }

                tuneSetPoint = tunePosition;
                loadSetPoint = loadPosition;
                positionOutOfToleranceSince = null;

                changed = snapshot with
                {
                    TuneSetPoint = tunePosition,
                    LoadSetPoint = loadPosition,
                    IsPositionOutOfTolerance =
                        false,
                    Timestamp =
                        DateTimeOffset.UtcNow
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
            $"RF matcher '{Id}' accepted manual " +
            $"positions: Tune={tunePosition}, " +
            $"Load={loadPosition} " +
            $"{Definition.PositionUnit}.");
    }

    private bool IsPositionValid(double position)
    {
        return double.IsFinite(position) &&
               position >=
                   Definition.MinimumPosition &&
               position <=
                   Definition.MaximumPosition;
    }

    public override sealed async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfMatcherSnapshot? snapshotToPublish = null;

        try
        {
            RfMatcherMode mode;
            double tuneTarget;
            double loadTarget;

            lock (syncRoot)
            {
                mode = commandedMode;
                tuneTarget = tuneSetPoint;
                loadTarget = loadSetPoint;
            }

            var result =
                await ExchangeAsync(
                        mode,
                        tuneTarget,
                        loadTarget,
                        cancellationToken)
                    .ConfigureAwait(false);

            var positionOutOfTolerance =
                CalculatePositionOutOfTolerance(
                    mode,
                    tuneTarget,
                    loadTarget,
                    result,
                    result.Timestamp);

            var diagnosticCode =
                GetDiagnosticCode(
                    result,
                    positionOutOfTolerance);

            var changed =
                new RfMatcherSnapshot
                {
                    Id = Definition.Id,
                    CommandedMode = mode,
                    ActualMode =
                        result.ActualMode,
                    TuneSetPoint = tuneTarget,
                    TunePosition =
                        result.TunePosition,
                    LoadSetPoint = loadTarget,
                    LoadPosition =
                        result.LoadPosition,
                    PositionUnit =
                        Definition.PositionUnit,
                    IsTuning = result.IsTuning,
                    IsMatched = result.IsMatched,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsFaulted = result.IsFaulted,
                    IsPositionOutOfTolerance =
                        positionOutOfTolerance,
                    DiagnosticCode =
                        diagnosticCode,
                    Timestamp = result.Timestamp
                };

            UpdateSnapshot(changed);
            snapshotToPublish = changed;
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            positionOutOfToleranceSince = null;

            var failed = Snapshot with
            {
                ActualMode =
                    RfMatcherMode.Unknown,
                IsTuning = false,
                IsMatched = false,
                IsOffline = true,
                IsFeedbackValid = false,
                IsPositionOutOfTolerance = false,
                DiagnosticCode =
                    RfMatcherDiagnosticCodes
                        .ExchangeFailed,
                Timestamp = DateTimeOffset.UtcNow
            };

            UpdateSnapshot(failed);
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

    public override sealed async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfMatcherSnapshot? snapshotToPublish = null;

        try
        {
            await ShutdownCoreAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                snapshotToPublish = snapshot with
                {
                    ActualMode = RfMatcherMode.Unknown,
                    IsTuning = false,
                    IsMatched = false,
                    IsOffline = true,
                    IsFeedbackValid = false,
                    IsPositionOutOfTolerance = false,
                    DiagnosticCode =
                        RfMatcherDiagnosticCodes.Offline,
                    Timestamp = DateTimeOffset.UtcNow
                };

                snapshot = snapshotToPublish;
            }
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

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool CalculatePositionOutOfTolerance(
        RfMatcherMode mode,
        double tuneTarget,
        double loadTarget,
        RfMatcherCycleResult result,
        DateTimeOffset now)
    {
        var tuneDeviation =
            Math.Abs(
                result.TunePosition -
                tuneTarget);

        var loadDeviation =
            Math.Abs(
                result.LoadPosition -
                loadTarget);

        var exceedsTolerance =
            mode == RfMatcherMode.Manual &&
            result.ActualMode ==
                RfMatcherMode.Manual &&
            !result.IsOffline &&
            result.IsFeedbackValid &&
            !result.IsFaulted &&
            (tuneDeviation >
                 Definition.PositionTolerance ||
             loadDeviation >
                 Definition.PositionTolerance);

        if (!exceedsTolerance)
        {
            positionOutOfToleranceSince = null;
            return false;
        }

        positionOutOfToleranceSince ??= now;

        return now -
            positionOutOfToleranceSince.Value >=
            Definition.PositionToleranceDelay;
    }

    private static string? GetDiagnosticCode(
        RfMatcherCycleResult result,
        bool positionOutOfTolerance)
    {
        if (result.IsOffline)
        {
            return RfMatcherDiagnosticCodes
                .Offline;
        }

        if (result.IsFaulted)
        {
            return RfMatcherDiagnosticCodes
                .DeviceFault;
        }

        if (!result.IsFeedbackValid)
        {
            return RfMatcherDiagnosticCodes
                .FeedbackBadQuality;
        }

        if (positionOutOfTolerance)
        {
            return RfMatcherDiagnosticCodes
                .PositionOutOfTolerance;
        }

        return result.DiagnosticCode;
    }

    private void UpdateSnapshot(RfMatcherSnapshot current)
    {
        lock (syncRoot)
        {
            snapshot = current;
        }

    }

    private void PublishSnapshotChanged(RfMatcherSnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<
                RfMatcherSnapshot>>()
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
                // 后续统一接入硬件诊断日志出口。
            }
        }
    }

    protected abstract ValueTask<RfMatcherCycleResult> ExchangeAsync(
        RfMatcherMode commandedMode,
        double tuneSetPoint,
        double loadSetPoint,
        CancellationToken cancellationToken);

    protected readonly record struct RfMatcherCycleResult(
        RfMatcherMode ActualMode,
        double TunePosition,
        double LoadPosition,
        bool IsTuning,
        bool IsMatched,
        bool IsOffline,
        bool IsFeedbackValid,
        bool IsFaulted,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}
