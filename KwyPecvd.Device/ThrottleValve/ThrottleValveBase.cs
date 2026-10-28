namespace KwyPecvd.Device.ThrottleValve;

public abstract class ThrottleValveBase : HardwareComponentBase, IThrottleValve
{
    private readonly object syncRoot = new();

    private readonly SemaphoreSlim operationGate = new(1, 1);
    private ThrottleValveMode commandedMode;

    /// <summary>
    /// 已经发现危险状态，但安全命令尚未成功下发。
    /// </summary>
    private bool safeModePending;
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

        commandedMode = definition.SafeMode;

        positionSetPoint = definition.MinimumPosition;

        pressureSetPoint = 0;

        snapshot = new ThrottleValveSnapshot
        {
            Id = definition.Id,

            CommandedMode = definition.SafeMode,

            ActualMode = ThrottleValveMode.Unknown,

            PositionSetPoint = positionSetPoint,

            PositionFeedback = definition.MinimumPosition,

            PositionUnit = definition.PositionUnit,

            PressureSetPoint = pressureSetPoint,

            PressureFeedback = 0,

            PressureUnit = definition.PressureUnit,

            IsOffline = true,

            IsPositionFeedbackValid = false,

            IsPressureFeedbackValid = false,

            IsFaulted = false,

            IsPositionOutOfTolerance = false,

            IsPressureOutOfTolerance = false,

            DiagnosticCode = ThrottleValveDiagnosticCodes.NotSampled,

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

        var safetyCommandAttempted = false;
        var safetyCommandSucceeded = false;

        try
        {
            var result =
                await ReadFeedbackAsync(cancellationToken)
                    .ConfigureAwait(false);

            ThrottleValveMode requestedMode;
            double requestedPosition;
            double requestedPressure;

            lock (syncRoot)
            {
                requestedMode = commandedMode;
                requestedPosition = positionSetPoint;
                requestedPressure = pressureSetPoint;
            }

            var feedbackUsable =
                IsFeedbackUsable(
                    requestedMode,
                    result);

            lock (syncRoot)
            {
                if (!feedbackUsable &&
                    requestedMode != Definition.SafeMode)
                {
                    safeModePending = true;
                }
            }

            bool shouldWriteSafeMode;

            lock (syncRoot)
            {
                shouldWriteSafeMode =
                    safeModePending &&
                    requestedMode !=
                        Definition.SafeMode;
            }

            if (shouldWriteSafeMode)
            {
                safetyCommandAttempted = true;

                await WriteSafeModeAsync(cancellationToken)
                    .ConfigureAwait(false);

                safetyCommandSucceeded = true;
                requestedMode = Definition.SafeMode;

                lock (syncRoot)
                {
                    commandedMode =
                        Definition.SafeMode;

                    safeModePending = false;

                    positionOutOfToleranceSince = null;
                    pressureOutOfToleranceSince = null;
                }
            }

            var positionOutOfTolerance =
                CalculateOutOfTolerance(
                    shouldEvaluate:
                        feedbackUsable &&
                        requestedMode ==
                            ThrottleValveMode.Position &&
                        result.ActualMode ==
                            ThrottleValveMode.Position,

                    setPoint: requestedPosition,
                    feedback: result.PositionFeedback,
                    tolerance:
                        Definition.PositionTolerance,
                    toleranceDelay:
                        Definition.ToleranceDelay,
                    now: result.Timestamp,
                    outOfToleranceSince:
                        ref positionOutOfToleranceSince);

            var pressureOutOfTolerance =
                CalculateOutOfTolerance(
                    shouldEvaluate:
                        feedbackUsable &&
                        requestedMode ==
                            ThrottleValveMode.Pressure &&
                        result.ActualMode ==
                            ThrottleValveMode.Pressure,

                    setPoint: requestedPressure,
                    feedback: result.PressureFeedback,
                    tolerance:
                        Definition.PressureTolerance,
                    toleranceDelay:
                        Definition.ToleranceDelay,
                    now: result.Timestamp,
                    outOfToleranceSince:
                        ref pressureOutOfToleranceSince);

            string? diagnosticCode = null;

            if (result.IsOffline)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.Offline;
            }
            else if (!feedbackUsable &&
                     !result.IsFaulted)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.FeedbackBadQuality;
            }
            else if (result.IsFaulted)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.DeviceFault;
            }
            else if (positionOutOfTolerance)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.PositionOutOfTolerance;
            }
            else if (pressureOutOfTolerance)
            {
                diagnosticCode = ThrottleValveDiagnosticCodes.PressureOutOfTolerance;
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

                    IsPositionFeedbackValid =
                        result.IsPositionFeedbackValid,

                    IsPressureFeedbackValid =
                        result.IsPressureFeedbackValid,

                    IsFaulted = result.IsFaulted,

                    IsPositionOutOfTolerance = positionOutOfTolerance,

                    IsPressureOutOfTolerance = pressureOutOfTolerance,

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
            ThrottleValveMode mode;

            lock (syncRoot)
            {
                mode = commandedMode;

                if (mode != Definition.SafeMode)
                {
                    safeModePending = true;
                }
            }
            // 如果读取反馈本身抛出异常，此前还没有机会
            // 执行安全命令，则在这里尝试一次。
            if (!safetyCommandAttempted &&
                mode != Definition.SafeMode)
            {
                try
                {
                    safetyCommandAttempted = true;

                    await WriteSafeModeAsync(
                            cancellationToken)
                        .ConfigureAwait(false);

                    safetyCommandSucceeded = true;
                }
                catch
                {
                    // 保留最初的通信/读取异常。
                }
            }

            lock (syncRoot)
            {
                if (safetyCommandSucceeded)
                {
                    commandedMode =
                        Definition.SafeMode;

                    safeModePending = false;
                }

                positionOutOfToleranceSince = null;
                pressureOutOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedMode = commandedMode,

                    ActualMode = ThrottleValveMode.Unknown,

                    IsOffline = true,

                    IsPositionFeedbackValid = false,

                    IsPressureFeedbackValid = false,

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

    protected virtual Task ShutdownCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

    public override sealed async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        ThrottleValveSnapshot? changed = null;

        try
        {
            await WriteSafeModeAsync(cancellationToken)
                .ConfigureAwait(false);

            lock (syncRoot)
            {
                commandedMode = Definition.SafeMode;

                safeModePending = false;

                positionOutOfToleranceSince = null;
                pressureOutOfToleranceSince = null;

                changed = snapshot with
                {
                    CommandedMode =
                        Definition.SafeMode,

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
            try
            {
                await ShutdownCoreAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
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
                if (safeModePending &&
                    mode != Definition.SafeMode)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Throttle valve '{Id}' has a " +
                        "pending safe-mode command.");
                }

                if (requiresReadyFeedback)
                {
                    var rejected =
                        GetCommandRejection(mode);

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
                if (mode == Definition.SafeMode)
                {
                    safeModePending = false;
                }

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
    private CommandResult? GetCommandRejection(
        ThrottleValveMode requestedMode)
    {
        if (snapshot.IsOffline)
        {
            return CommandResult.Failure(
                ResultCodes.Offline,
                $"Throttle valve '{Id}' is offline.");
        }

        var feedbackValid =
            requestedMode ==
                ThrottleValveMode.Pressure
                ? snapshot
                    .IsPressureFeedbackValid
                : snapshot
                    .IsPositionFeedbackValid;

        if (!feedbackValid)
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

    private static bool CalculateOutOfTolerance(
        bool shouldEvaluate,
        double setPoint,
        double feedback,
        double tolerance,
        TimeSpan toleranceDelay,
        DateTimeOffset now,
        ref DateTimeOffset? outOfToleranceSince)
    {
        if (!shouldEvaluate ||
            Math.Abs(feedback - setPoint) <= tolerance)
        {
            outOfToleranceSince = null;
            return false;
        }

        outOfToleranceSince ??= now;

        return now - outOfToleranceSince >= toleranceDelay;
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

        var requiredFeedbackValid =
            mode == ThrottleValveMode.Pressure
                ? snapshot
                    .IsPressureFeedbackValid
                : snapshot
                    .IsPositionFeedbackValid;

        // 离线、当前模式所需反馈无效或故障时，
        // 不能仅凭 Commanded 判断硬件已经执行命令。
        if (snapshot.IsOffline ||
            !requiredFeedbackValid ||
            snapshot.IsFaulted)
        {
            return false;
        }

        return mode switch
        {
            ThrottleValveMode.Closed =>
                snapshot.ActualMode ==
                    ThrottleValveMode.Closed,

            ThrottleValveMode.Open =>
                snapshot.ActualMode ==
                    ThrottleValveMode.Open,

            ThrottleValveMode.Position =>
                requestedPosition.HasValue &&
                snapshot.ActualMode ==
                    ThrottleValveMode.Position &&
                positionSetPoint ==
                    requestedPosition.Value,

            ThrottleValveMode.Pressure =>
                requestedPressure.HasValue &&
                snapshot.ActualMode ==
                    ThrottleValveMode.Pressure &&
                pressureSetPoint ==
                    requestedPressure.Value,

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

    private ValueTask WriteSafeModeAsync(CancellationToken cancellationToken)
    {
        return Definition.SafeMode switch
        {
            ThrottleValveMode.Closed =>
                WriteCloseAsync(cancellationToken),

            ThrottleValveMode.Open =>
                WriteOpenAsync(cancellationToken),

            _ => throw new InvalidOperationException(
                $"Throttle valve '{Id}' has an " +
                "invalid safe mode.")
        };
    }

    private static bool IsFeedbackUsable(
        ThrottleValveMode requestedMode,
        ThrottleValveCycleResult result)
    {
        if (result.IsOffline ||
            result.IsFaulted)
        {
            return false;
        }

        return requestedMode ==
            ThrottleValveMode.Pressure
                ? result.IsPressureFeedbackValid
                : result.IsPositionFeedbackValid;
    }

    protected abstract ValueTask<ThrottleValveCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken);

    protected readonly record struct ThrottleValveCycleResult(
        ThrottleValveMode ActualMode,
        double PositionFeedback,
        double PressureFeedback,
        bool IsOffline,
        bool IsPositionFeedbackValid,
        bool IsPressureFeedbackValid,
        bool IsFaulted,
        DateTimeOffset Timestamp);
}
