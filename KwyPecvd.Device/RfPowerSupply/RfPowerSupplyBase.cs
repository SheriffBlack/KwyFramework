namespace KwyPecvd.Device.RfPowerSupply;

public abstract class RfPowerSupplyBase : HardwareComponentBase, IRfPowerSupply
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);

    private double powerSetPoint;

    private bool commandedEnabled;

    private DateTimeOffset? outOfToleranceSince;

    private DateTimeOffset? reflectedPowerHighSince;

    private RfPowerSupplySnapshot snapshot;

    protected RfPowerSupplyBase(RfPowerSupplyDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        snapshot = new RfPowerSupplySnapshot
        {
            Id = definition.Id,
            SetPoint = 0,
            ForwardPower = 0,
            ReflectedPower = 0,
            Unit = definition.Unit,
            CommandedEnabled = false,
            IsOutputOn = false,
            IsOffline = true,
            IsFeedbackValid = false,
            IsFaulted = false,
            IsReflectedPowerHigh = false,
            IsOutOfTolerance = false,
            DiagnosticCode =
                RfPowerSupplyDiagnosticCodes
                    .NotSampled,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public RfPowerSupplyDefinition Definition { get; }

    public RfPowerSupplySnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<RfPowerSupplySnapshot>? SnapshotChanged;

    public override sealed async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<CommandResult> SetPowerAsync(
        double power,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(power))
        {
            return CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "RF power must be finite.");
        }

        if (power < 0 ||
            power > Definition.MaximumPower)
        {
            return CommandResult.Failure(
                ResultCodes.OutOfRange,
                $"RF power supply '{Id}' power " +
                $"must be between 0 and " +
                $"{Definition.MaximumPower} " +
                $"{Definition.Unit}.");
        }

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfPowerSupplySnapshot changed;

        try
        {
            lock (syncRoot)
            {
                powerSetPoint = power;
                outOfToleranceSince = null;

                changed = snapshot with
                {
                    SetPoint = power,
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
            $"RF power supply '{Id}' accepted " +
            $"power {power} {Definition.Unit}.");
    }

    public async Task<CommandResult> EnableAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfPowerSupplySnapshot changed;

        try
        {
            lock (syncRoot)
            {
                if (powerSetPoint <= 0)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"RF power supply '{Id}' " +
                        "power must be greater than zero.");
                }

                if (snapshot.IsOffline)
                {
                    return CommandResult.Failure(
                        ResultCodes.Offline,
                        $"RF power supply '{Id}' " +
                        "is offline.");
                }

                if (!snapshot.IsFeedbackValid)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"RF power supply '{Id}' " +
                        "feedback is invalid.");
                }

                if (snapshot.IsFaulted)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"RF power supply '{Id}' " +
                        "has a generator fault.");
                }

                if (snapshot.IsReflectedPowerHigh)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"RF power supply '{Id}' " +
                        "reflected power is high.");
                }

                commandedEnabled = true;
                outOfToleranceSince = null;
                reflectedPowerHighSince = null;

                changed = snapshot with
                {
                    CommandedEnabled = true,
                    IsOutOfTolerance = false,
                    IsReflectedPowerHigh = false,
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
            $"RF power supply '{Id}' accepted " +
            "enable command.");
    }

    public async Task<CommandResult> DisableAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfPowerSupplySnapshot changed;

        try
        {
            lock (syncRoot)
            {
                commandedEnabled = false;
                outOfToleranceSince = null;
                reflectedPowerHighSince = null;

                changed = snapshot with
                {
                    CommandedEnabled = false,
                    IsOutOfTolerance = false,
                    IsReflectedPowerHigh = false,
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
            $"RF power supply '{Id}' accepted " +
            "disable command.");
    }

    public override sealed async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfPowerSupplySnapshot? snapshotToPublish = null;

        try
        {
            double setPoint;
            bool requestedEnabled;

            lock (syncRoot)
            {
                setPoint = powerSetPoint;
                requestedEnabled = commandedEnabled;
            }

            var result =
                await ExchangeAsync(
                        setPoint,
                        requestedEnabled,
                        cancellationToken)
                    .ConfigureAwait(false);

            var reflectedPowerHigh =
                CalculateReflectedPowerHigh(
                    result.ReflectedPower,
                    requestedEnabled,
                    result.IsOutputOn,
                    result.IsOffline,
                    result.IsFeedbackValid,
                    result.Timestamp);

            var outOfTolerance =
                CalculateOutOfTolerance(
                    setPoint,
                    result.ForwardPower,
                    requestedEnabled,
                    result.IsOutputOn,
                    result.IsOffline,
                    result.IsFeedbackValid,
                    result.Timestamp);

            // 严重状态出现后撤销软件输出请求。
            var effectiveEnabled =
                requestedEnabled &&
                !result.IsOffline &&
                result.IsFeedbackValid &&
                !result.IsFaulted &&
                !reflectedPowerHigh;

            var diagnosticCode =
                GetDiagnosticCode(
                    result,
                    reflectedPowerHigh,
                    outOfTolerance);

            var changed =
                new RfPowerSupplySnapshot
                {
                    Id = Definition.Id,
                    SetPoint = setPoint,
                    ForwardPower =
                        result.ForwardPower,
                    ReflectedPower =
                        result.ReflectedPower,
                    Unit = Definition.Unit,
                    CommandedEnabled =
                        effectiveEnabled,
                    IsOutputOn = result.IsOutputOn,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsFaulted = result.IsFaulted,
                    IsReflectedPowerHigh =
                        reflectedPowerHigh,
                    IsOutOfTolerance =
                        outOfTolerance,
                    DiagnosticCode =
                        diagnosticCode,
                    Timestamp = result.Timestamp
                };

            lock (syncRoot)
            {
                commandedEnabled = effectiveEnabled;
                snapshot = changed;
            }

            snapshotToPublish = changed;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            RfPowerSupplySnapshot failed;

            lock (syncRoot)
            {
                commandedEnabled = false;
                outOfToleranceSince = null;
                reflectedPowerHighSince = null;

                failed = snapshot with
                {
                    CommandedEnabled = false,
                    IsOffline = true,
                    IsFeedbackValid = false,
                    IsOutOfTolerance = false,
                    DiagnosticCode =
                        RfPowerSupplyDiagnosticCodes
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

    private bool CalculateReflectedPowerHigh(
        double reflectedPower,
        bool enabled,
        bool outputOn,
        bool offline,
        bool feedbackValid,
        DateTimeOffset now)
    {
        var exceedsLimit =
            enabled &&
            outputOn &&
            !offline &&
            feedbackValid &&
            reflectedPower >
                Definition.ReflectedPowerLimit;

        if (!exceedsLimit)
        {
            reflectedPowerHighSince = null;
            return false;
        }

        reflectedPowerHighSince ??= now;

        return now -
            reflectedPowerHighSince.Value >=
            Definition.ReflectedPowerDelay;
    }

    private bool CalculateOutOfTolerance(
        double setPoint,
        double forwardPower,
        bool enabled,
        bool outputOn,
        bool offline,
        bool feedbackValid,
        DateTimeOffset now)
    {
        var exceedsTolerance =
            enabled &&
            outputOn &&
            !offline &&
            feedbackValid &&
            setPoint > 0 &&
            Math.Abs(forwardPower - setPoint) >
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

    private static string? GetDiagnosticCode(
        RfPowerSupplyCycleResult result,
        bool reflectedPowerHigh,
        bool outOfTolerance)
    {
        if (result.IsOffline)
        {
            return RfPowerSupplyDiagnosticCodes
                .Offline;
        }

        if (result.IsFaulted)
        {
            return RfPowerSupplyDiagnosticCodes
                .GeneratorFault;
        }

        if (reflectedPowerHigh)
        {
            return RfPowerSupplyDiagnosticCodes
                .ReflectedPowerHigh;
        }

        if (!result.IsFeedbackValid)
        {
            return RfPowerSupplyDiagnosticCodes
                .FeedbackBadQuality;
        }

        if (outOfTolerance)
        {
            return RfPowerSupplyDiagnosticCodes
                .ForwardPowerOutOfTolerance;
        }

        return result.DiagnosticCode;
    }

    public override sealed async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        RfPowerSupplySnapshot? snapshotToPublish = null;

        try
        {
            var result =
                await ExchangeAsync(
                        powerSetPoint: 0,
                        enabled: false,
                        cancellationToken)
                    .ConfigureAwait(false);

            var changed =
                new RfPowerSupplySnapshot
                {
                    Id = Definition.Id,
                    SetPoint = 0,
                    ForwardPower =
                        result.ForwardPower,
                    ReflectedPower =
                        result.ReflectedPower,
                    Unit = Definition.Unit,
                    CommandedEnabled = false,
                    IsOutputOn = result.IsOutputOn,
                    IsOffline = result.IsOffline,
                    IsFeedbackValid =
                        result.IsFeedbackValid,
                    IsFaulted = result.IsFaulted,
                    IsReflectedPowerHigh = false,
                    IsOutOfTolerance = false,
                    DiagnosticCode =
                        result.DiagnosticCode,
                    Timestamp = result.Timestamp
                };

            lock (syncRoot)
            {
                powerSetPoint = 0;
                commandedEnabled = false;
                outOfToleranceSince = null;
                reflectedPowerHighSince = null;
                snapshot = changed;
            }

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

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void PublishSnapshotChanged(RfPowerSupplySnapshot current)
    {
        var handlers = SnapshotChanged?
            .GetInvocationList()
            .Cast<EventHandler<
                RfPowerSupplySnapshot>>()
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

    protected abstract ValueTask<RfPowerSupplyCycleResult> ExchangeAsync(
        double powerSetPoint,
        bool enabled,
        CancellationToken cancellationToken);

    protected readonly record struct RfPowerSupplyCycleResult(
        double ForwardPower,
        double ReflectedPower,
        bool IsOutputOn,
        bool IsOffline,
        bool IsFeedbackValid,
        bool IsFaulted,
        DateTimeOffset Timestamp,
        string? DiagnosticCode = null);
}
