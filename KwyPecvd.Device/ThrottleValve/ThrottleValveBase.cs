namespace KwyPecvd.Device.ThrottleValve;

public abstract class ThrottleValveBase : HardwareComponentBase, IThrottleValve
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);

    private ThrottleValveMode commandedMode = ThrottleValveMode.Closed;

    private double positionSetPoint;

    private double pressureSetPoint;

    private DateTimeOffset? positionOutOfToleranceSince;

    private DateTimeOffset? pressureOutOfToleranceSince;

    private ThrottleValveSnapshot snapshot;

    protected ThrottleValveBase(ThrottleValveDefinition definition)
        : base(
            definition?.Id ??
            throw new ArgumentNullException(
                nameof(definition)))
    {
        definition.Validate();

        Definition = definition;

        positionSetPoint = definition.MinimumPosition;

        pressureSetPoint = 0;

        snapshot = new ThrottleValveSnapshot
        {
            Id = definition.Id,

            CommandedMode = ThrottleValveMode.Closed,

            ActualMode = ThrottleValveMode.Unknown,

            PositionSetPoint = positionSetPoint,

            PositionFeedback = definition.MinimumPosition,

            PositionUnit = definition.PositionUnit,

            PressureSetPoint = pressureSetPoint,

            PressureFeedback = 0,

            PressureUnit = definition.PressureUnit,

            IsOffline = true,

            IsFeedbackValid = false,

            IsFaulted = false,

            IsPositionOutOfTolerance = false,

            IsPressureOutOfTolerance = false,

            DiagnosticCode = ThrottleValveDiagnosticCodes .NotSampled,

            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public ThrottleValveDefinition Definition { get; }

    public ThrottleValveSnapshot Snapshot
    {
        get
        {
            lock (syncRoot)
            {
                return snapshot;
            }
        }
    }

    public event EventHandler<ThrottleValveSnapshot>? SnapshotChanged;

    public override sealed async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Definition.Validate();

        await InitializeCoreAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public override sealed async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        ThrottleValveSnapshot? changed = null;

        try
        {
            var result =
                await ReadFeedbackAsync(cancellationToken)
                    .ConfigureAwait(false);

            string? diagnosticCode = null;

            if (result.IsOffline)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.Offline;
            }
            else if (!result.IsFeedbackValid)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.FeedbackBadQuality;
            }
            else if (result.IsFaulted)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.DeviceFault;
            }

            lock (syncRoot)
            {
                changed = new ThrottleValveSnapshot
                {
                    Id = Definition.Id,

                    CommandedMode = commandedMode,

                    ActualMode = result.ActualMode,

                    PositionSetPoint = positionSetPoint,

                    PositionFeedback = result.PositionFeedback,

                    PositionUnit = Definition.PositionUnit,

                    PressureSetPoint = pressureSetPoint,

                    PressureFeedback = result.PressureFeedback,

                    PressureUnit = Definition.PressureUnit,

                    IsOffline = result.IsOffline,

                    IsFeedbackValid = result.IsFeedbackValid,

                    IsFaulted = result.IsFaulted,

                    IsPositionOutOfTolerance = false,

                    IsPressureOutOfTolerance = false,

                    DiagnosticCode = diagnosticCode,

                    Timestamp = result.Timestamp
                };

                snapshot = changed;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            lock (syncRoot)
            {
                positionOutOfToleranceSince = null;
                pressureOutOfToleranceSince = null;

                changed = snapshot with
                {
                    ActualMode = ThrottleValveMode.Unknown,

                    IsOffline = true,

                    IsFeedbackValid = false,

                    IsPositionOutOfTolerance = false,

                    IsPressureOutOfTolerance = false,

                    DiagnosticCode = ThrottleValveDiagnosticCodes.ExchangeFailed,

                    Timestamp = DateTimeOffset.UtcNow
                };

                snapshot = changed;
            }

            throw;
        }
        finally
        {
            operationGate.Release();

            if (changed is not null)
            {
                PublishSnapshotChanged(changed);
            }
        }
    }

    protected virtual Task InitializeCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected abstract ValueTask WriteCloseAsync(CancellationToken cancellationToken);

    protected abstract ValueTask WriteOpenAsync(CancellationToken cancellationToken);

    protected abstract ValueTask WritePositionAsync(
        double position,
        CancellationToken cancellationToken);

    protected abstract ValueTask WritePressureAsync(
        double pressure,
        CancellationToken cancellationToken);

    public Task<CommandResult> CloseAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(
            ThrottleValveMode.Closed,
            WriteCloseAsync,
            positionSetPoint: null,
            pressureSetPoint: null,
            requiresReadyFeedback: false,
            cancellationToken);

    public Task<CommandResult> OpenAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(
            ThrottleValveMode.Open,
            WriteOpenAsync,
            positionSetPoint: null,
            pressureSetPoint: null,
            requiresReadyFeedback: true,
            cancellationToken);

    /// <summary>实现位置控制 </summary>
    public Task<CommandResult> SetPositionAsync(
        double position,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(position) ||
            position < Definition.MinimumPosition ||
            position > Definition.MaximumPosition)
        {
            return Task.FromResult(
                CommandResult.Failure(
                    ResultCodes.OutOfRange,
                    $"Throttle valve '{Id}' position " +
                    $"must be between " +
                    $"{Definition.MinimumPosition} and " +
                    $"{Definition.MaximumPosition} " +
                    $"{Definition.PositionUnit}."));
        }

        return ExecuteCommandAsync(
            ThrottleValveMode.Position,
            token => WritePositionAsync(
                position,
                token),
            positionSetPoint: position,
            pressureSetPoint: null,
            requiresReadyFeedback: true,
            cancellationToken);
    }

    /// <summary>实现压力控制 </summary>
    public Task<CommandResult> SetPressureAsync(
        double pressure,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(pressure) ||
            pressure < 0 ||
            pressure > Definition.MaximumPressure)
        {
            return Task.FromResult(
                CommandResult.Failure(
                    ResultCodes.OutOfRange,
                    $"Throttle valve '{Id}' pressure " +
                    $"must be between 0 and " +
                    $"{Definition.MaximumPressure} " +
                    $"{Definition.PressureUnit}."));
        }

        return ExecuteCommandAsync(
            ThrottleValveMode.Pressure,
            token => WritePressureAsync(
                pressure,
                token),
            positionSetPoint: null,
            pressureSetPoint: pressure,
            requiresReadyFeedback: true,
            cancellationToken);
    }

    /// <summary>统一命令入口 </summary>
    private async Task<CommandResult> ExecuteCommandAsync(
        ThrottleValveMode mode,
        Func<CancellationToken, ValueTask>
            writeCommandAsync,
        double? positionSetPoint,
        double? pressureSetPoint,
        bool requiresReadyFeedback,
        CancellationToken cancellationToken)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        ThrottleValveSnapshot? changed = null;

        try
        {
            lock (syncRoot)
            {
                if (requiresReadyFeedback)
                {
                    var rejected =
                        GetCommandRejection();

                    if (rejected is not null)
                    {
                        return rejected;
                    }
                }

                if (IsSameCommand(
                        mode,
                        positionSetPoint,
                        pressureSetPoint))
                {
                    return CommandResult.Success(
                        $"Throttle valve '{Id}' " +
                        "already accepted the command.");
                }
            }

            await writeCommandAsync(cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                commandedMode = mode;

                if (positionSetPoint.HasValue)
                {
                    this.positionSetPoint =
                        positionSetPoint.Value;
                }

                if (pressureSetPoint.HasValue)
                {
                    this.pressureSetPoint =
                        pressureSetPoint.Value;
                }

                positionOutOfToleranceSince = null;
                pressureOutOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedMode = mode,

                    PositionSetPoint =
                        this.positionSetPoint,

                    PressureSetPoint =
                        this.pressureSetPoint,

                    IsPositionOutOfTolerance =
                        false,

                    IsPressureOutOfTolerance =
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
            $"Throttle valve '{Id}' accepted " +
            $"mode '{mode}'.");
    }

    /// <summary>就绪检查</summary>
    private CommandResult? GetCommandRejection()
    {
        if (snapshot.IsOffline)
        {
            return CommandResult.Failure(
                ResultCodes.Offline,
                $"Throttle valve '{Id}' is offline.");
        }

        if (!snapshot.IsFeedbackValid)
        {
            return CommandResult.Failure(
                ResultCodes.NotReady,
                $"Throttle valve '{Id}' feedback " +
                "is invalid.");
        }

        if (snapshot.IsFaulted)
        {
            return CommandResult.Failure(
                ResultCodes.NotReady,
                $"Throttle valve '{Id}' has a fault.");
        }

        return null;
    }

    /// <summary>幂等判断 </summary>
    private bool IsSameCommand(
        ThrottleValveMode mode,
        double? requestedPosition,
        double? requestedPressure)
    {
        if (commandedMode != mode)
        {
            return false;
        }

        return mode switch
        {
            ThrottleValveMode.Position =>
                requestedPosition.HasValue &&
                positionSetPoint ==
                    requestedPosition.Value,

            ThrottleValveMode.Pressure =>
                requestedPressure.HasValue &&
                pressureSetPoint ==
                    requestedPressure.Value,

            ThrottleValveMode.Open or
            ThrottleValveMode.Closed =>
                true,

            _ => false
        };
    }


    private void PublishSnapshotChanged(ThrottleValveSnapshot current)
    {
        var handlers = SnapshotChanged;

        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<
                     ThrottleValveSnapshot> handler
                 in handlers.GetInvocationList()
                     .Cast<EventHandler<
                         ThrottleValveSnapshot>>())
        {
            try
            {
                handler(this, current);
            }
            catch
            {
                // 观察者异常不能中断硬件命令。
            }
        }
    }

    protected abstract ValueTask<ThrottleValveCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken);

    protected readonly record struct ThrottleValveCycleResult(
        ThrottleValveMode ActualMode,
        double PositionFeedback,
        double PressureFeedback,
        bool IsOffline,
        bool IsFeedbackValid,
        bool IsFaulted,
        DateTimeOffset Timestamp);
}
