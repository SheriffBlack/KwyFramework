using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;

namespace KwyPecvd.Process.Chambers;

/// <summary>
/// 负责腔体状态和高层操作编排。
/// 状态转换
///     → 调用气路系统
///     → 处理操作结果
/// </summary>
public sealed class ChamberController : IChamberController
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
    private readonly ChamberGasSystem gasSystem;
    private readonly ChamberVacuumSystem vacuumSystem;
    private ChamberRuntimeState runtime = new();

    private readonly ChamberInterlockEvaluator interlockEvaluator;

    public ChamberController(
        ChamberDefinition definition,
        IHardwareComponentRegistry registry,
        IChamberInterlockSource interlockSource)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(interlockSource);

        definition.Validate();

        Definition = definition;

        gasSystem = new ChamberGasSystem(
            definition.Id,
            definition.GasLines,
            registry);

        vacuumSystem = new ChamberVacuumSystem(
            definition.Id,
            definition.Vacuum,
            registry);

        interlockEvaluator =new ChamberInterlockEvaluator(
            definition.Id,
            interlockSource);
    }

    public string Id => Definition.Id;

    public ChamberDefinition Definition { get; }

    public ChamberSnapshot Snapshot
    {
        get
        {
            var current = GetRuntimeState();

            return new ChamberSnapshot
            {
                ChamberId = Id,
                State = current.State,
                StateChangedAt = current.StateChangedAt,
                FaultCode = current.FaultCode,
                FaultMessage = current.FaultMessage,
                GasFlows = gasSystem.CreateSnapshots(),
                Vacuum = vacuumSystem.CreateSnapshot(),
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    public Task<CommandResult> InitializeAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => InitializeCoreAsync(cancellationToken), cancellationToken);

    public Task<CommandResult> SetGasFlowAsync(string gasName, double target,
        TimeSpan rampDuration, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => SetGasFlowCoreAsync(gasName, target, rampDuration, cancellationToken), cancellationToken);

    public Task<CommandResult> StopGasFlowAsync(string gasName, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => StopGasFlowCoreAsync(gasName, cancellationToken), cancellationToken);

    public Task<CommandResult> StartGasPreparationAsync(IReadOnlyCollection<ChamberGasTarget> targets,
        CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => StartGasPreparationCoreAsync(targets, cancellationToken), cancellationToken);

    public Task<CommandResult> StopProcessAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => StopProcessCoreAsync(cancellationToken), cancellationToken);

    public Task<CommandResult> ResetFaultAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => ResetFaultCoreAsync(cancellationToken), cancellationToken);

    private async Task<CommandResult> ExecuteCommandAsync(
        Func<Task<CommandResult>> command, CancellationToken cancellationToken)
    {
        // 不排队执行过时的操作；调用方可以根据最新状态重试。
        if (!await commandGate.WaitAsync(0, cancellationToken))
            return CommandResult.Failure(ResultCodes.InvalidState,
                $"Chamber '{Id}' is executing another command.");

        try { return await command(); }
        finally { commandGate.Release(); }
    }

    private Task<CommandResult> InitializeCoreAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBeginInitialization(out var failure))
        {
            return Task.FromResult(failure);
        }

        try
        {
            if (!gasSystem.TryValidateReady(out failure))
            {
                ChangeState(ChamberState.Offline);
                return Task.FromResult(failure);
            }

            if (!vacuumSystem.TryValidateReady(out failure))
            {
                ChangeState(ChamberState.Offline);
                return Task.FromResult(failure);
            }

            ChangeState(ChamberState.Idle);

            return Task.FromResult(
                CommandResult.Success(
                    $"Chamber '{Id}' initialized."));
        }
        catch (Exception exception)
        {
            ChangeState(
                ChamberState.Faulted,
                ChamberFaultCodes.InitializeFailed,
                exception.Message);

            return Task.FromResult(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    exception.Message));
        }
    }

    private Task<CommandResult> SetGasFlowCoreAsync(
        string gasName,
        double target,
        TimeSpan rampDuration,
        CancellationToken cancellationToken = default)
    {
        if (!gasSystem.TryResolveMfc(
                gasName,
                out var mfc,
                out var failure))
        {
            return Task.FromResult(failure);
        }

        if (!CanSetGasFlow(out failure))
        {
            return Task.FromResult(failure);
        }

        return mfc.SetFlowAsync(
            target,
            rampDuration,
            cancellationToken);
    }

    private Task<CommandResult> StopGasFlowCoreAsync(
        string gasName,
        CancellationToken cancellationToken = default)
    {
        if (!gasSystem.TryResolveMfc(
                gasName,
                out var mfc,
                out var failure))
        {
            return Task.FromResult(failure);
        }

        return mfc.StopAsync(cancellationToken);
    }

    private async Task<CommandResult> StopProcessCoreAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBeginStopping(out var failure))
        {
            return failure;
        }

        try
        {
            // 停止一经接受，由RT在独立超时范围内完成安全停止。
            using var stopCancellation = new CancellationTokenSource(StopTimeout);
            var result = await gasSystem.StopAllAsync(stopCancellation.Token);

            if (result.Succeeded)
            {
                lock (syncRoot)
                    runtime = runtime with { StopCommandsCompletedAt = DateTimeOffset.UtcNow };
                return result;
            }

            ChangeState(
                ChamberState.Faulted,
                ChamberFaultCodes.ProcessStopFailed,
                result.Message);

            return result;
        }
        catch (OperationCanceledException)
        {
            ChangeState(
                ChamberState.Faulted,
                ChamberFaultCodes.ProcessStopTimeout,
                "Process stop command timed out.");

            return CommandResult.Failure(ResultCodes.NotReady, "Process stop command timed out.");
        }
        catch (Exception exception)
        {
            ChangeState(
                ChamberState.Faulted,
                ChamberFaultCodes.ProcessStopFailed,
                exception.Message);

            return CommandResult.Failure(
                ResultCodes.NotReady,
                exception.Message);
        }
    }

    private async Task<CommandResult> StartGasPreparationCoreAsync(
        IReadOnlyCollection<ChamberGasTarget> targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        cancellationToken.ThrowIfCancellationRequested();

        if (!gasSystem.TryCreatePreparationPlan(
                targets,
                out var plan,
                out var failure))
        {
            return failure;
        }

        if (!interlockEvaluator.TryValidateGasPreparation(
            out failure))
        {
            return failure;
        }

        if (!TryBeginGasPreparation(
                plan.Targets,
                out failure))
        {
            return failure;
        }

        var acceptedMfcs = new List<IMfc>();

        try
        {
            foreach (var command in plan.Commands)
            {
                var result = await command.Mfc.SetFlowAsync(
                    command.Target.Target,
                    command.Target.RampDuration,
                    cancellationToken);

                if (!result.Succeeded)
                {
                    var compensationError =
                        await StopAcceptedMfcsAsync(acceptedMfcs);

                    var message = compensationError is null
                        ? result.Message
                        : $"{result.Message} Compensation: " +
                          compensationError;

                    ChangeState(
                        ChamberState.Faulted,
                        ChamberFaultCodes.GasPreparationFailed,
                        message);

                    return result with { Message = message };
                }

                acceptedMfcs.Add(command.Mfc);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            var compensationError =
                await StopAcceptedMfcsAsync(acceptedMfcs);

            if (compensationError is null)
            {
                ChangeState(ChamberState.Idle);
            }
            else
            {
                ChangeState(
                    ChamberState.Faulted,
                    ChamberFaultCodes.GasPreparationCancelFailed,
                    compensationError);
            }

            throw;
        }
        catch (Exception exception)
        {
            var compensationError =
                await StopAcceptedMfcsAsync(acceptedMfcs);

            var message = compensationError is null
                ? exception.Message
                : $"{exception.Message} Compensation: " +
                  compensationError;

            ChangeState(
                ChamberState.Faulted,
                ChamberFaultCodes.GasPreparationFailed,
                message);

            return CommandResult.Failure(
                ResultCodes.NotReady,
                message);
        }

        return CommandResult.Success(
            $"Chamber '{Id}' started gas preparation.");
    }

    private Task<CommandResult> ResetFaultCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            if (runtime.State !=
                ChamberState.Faulted)
            {
                return Task.FromResult(
                    CommandResult.Failure(
                        ResultCodes.InvalidState,
                        $"Chamber '{Id}' cannot reset " +
                        $"from state '{runtime.State}'."));
            }
        }

        if (!gasSystem.TryValidateReady(
                out var failure))
        {
            return Task.FromResult(failure);
        }

        ChangeState(ChamberState.Idle);

        return Task.FromResult(
            CommandResult.Success(
                $"Chamber '{Id}' fault was reset."));
    }

    public async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 命令下发期间不依据旧快照完成状态转换。
        if (!await commandGate.WaitAsync(0, cancellationToken))
            return;

        try
        {
            var current = GetRuntimeState();

            switch (current.State)
            {
                case ChamberState.Preparing:
                    EvaluatePreparation(current);
                    break;

                case ChamberState.Stopping:
                    EvaluateStopping(current);
                    break;
            }
        }
        finally { commandGate.Release(); }
    }

    private ChamberRuntimeState GetRuntimeState()
    {
        lock (syncRoot)
        {
            return runtime;
        }
    }

    private void EvaluatePreparation(ChamberRuntimeState current)
    {
        var evaluation =
            gasSystem.EvaluatePreparation(
                current.ActiveGasTargets);

        switch (evaluation.Status)
        {
            case GasPreparationStatus.Waiting:
                break;

            case GasPreparationStatus.Ready:
                TryTransition(
                    ChamberState.Preparing,
                    ChamberState.Processing);
                break;

            case GasPreparationStatus.Faulted:
                TryTransition(
                    ChamberState.Preparing,
                    ChamberState.Faulted,
                    evaluation.FaultCode,
                    evaluation.FaultMessage);
                break;
        }
    }

    private void EvaluateStopping(ChamberRuntimeState current)
    {
        if (current.StopCommandsCompletedAt is not { } completedAt)
            return;

        if (DateTimeOffset.UtcNow - completedAt >= StopTimeout)
        {
            TryTransition(ChamberState.Stopping, ChamberState.Faulted,
                ChamberFaultCodes.GasStopTimeout,
                "Gas feedback did not reach zero before the stop timeout.");
            return;
        }

        var evaluation =
            gasSystem.EvaluateStopped(completedAt);

        switch (evaluation.Status)
        {
            case GasStopStatus.Waiting:
                break;

            case GasStopStatus.Stopped:
                TryTransition(ChamberState.Stopping, ChamberState.Idle);
                break;

            case GasStopStatus.Faulted:
                TryTransition(
                    ChamberState.Stopping,
                    ChamberState.Faulted,
                    evaluation.FaultCode,
                    evaluation.FaultMessage);
                break;
        }
    }

    private bool TryTransition(
        ChamberState expectedState,
        ChamberState newState,
        string? faultCode = null,
        string? faultMessage = null)
    {
        lock (syncRoot)
        {
            if (runtime.State != expectedState)
            {
                return false;
            }

            SetStateUnsafe(
                newState,
                faultCode,
                faultMessage);

            return true;
        }
    }

    private bool TryBeginInitialization(
        out CommandResult failure)
    {
        lock (syncRoot)
        {
            if (runtime.State is not ChamberState.Offline and
                not ChamberState.Faulted)
            {
                failure = CommandResult.Failure(
                    ResultCodes.InvalidState,
                    $"Chamber '{Id}' cannot initialize " +
                    $"from state '{runtime.State}'.");
                return false;
            }

            runtime = runtime with
            {
                ActiveGasTargets =
                    ChamberRuntimeState.EmptyGasTargets
            };

            SetStateUnsafe(ChamberState.Initializing);
            failure = CommandResult.Success();
            return true;
        }
    }

    /// <summary>检查并进入气体准备状态。</summary>
    private bool TryBeginGasPreparation(
        IReadOnlyDictionary<string, ChamberGasTarget> targets,
        out CommandResult failure)
    {
        lock (syncRoot)
        {
            if (runtime.State != ChamberState.Idle)
            {
                failure = CommandResult.Failure(
                    ResultCodes.InvalidState,
                    $"Chamber '{Id}' cannot prepare gas " +
                    $"from state '{runtime.State}'.");
                return false;
            }

            runtime = runtime with
            {
                ActiveGasTargets = targets
            };

            SetStateUnsafe(ChamberState.Preparing);
            failure = CommandResult.Success();
            return true;
        }
    }

    private bool CanSetGasFlow(
        out CommandResult failure)
    {
        lock (syncRoot)
        {
            if (runtime.State is
                ChamberState.Idle or
                ChamberState.Preparing or
                ChamberState.Processing)
            {
                failure = CommandResult.Success();
                return true;
            }

            failure = CommandResult.Failure(
                ResultCodes.InvalidState,
                $"Chamber '{Id}' cannot set gas flow " +
                $"while in state '{runtime.State}'.");
            return false;
        }
    }

    private static async Task<string?> StopAcceptedMfcsAsync(
        IReadOnlyList<IMfc> acceptedMfcs)
    {
        List<string>? failures = null;

        for (var index = acceptedMfcs.Count - 1;
             index >= 0;
             index--)
        {
            var mfc = acceptedMfcs[index];

            var error = await ChamberGasSystem.TryStopMfcAsync(mfc, CancellationToken.None);
            if (error is not null)
                (failures ??= []).Add(error);
        }

        return failures is null
            ? null
            : string.Join("; ", failures);
    }

    /// <summary>腔体先原子地占据 Stopping 状态，其他启动或停止命令就无法插进来。</summary>
    private bool TryBeginStopping(out CommandResult failure)
    {
        lock (syncRoot)
        {
            if (runtime.State is not
                ChamberState.Preparing and not
                ChamberState.Processing)
            {
                failure = CommandResult.Failure(
                    ResultCodes.InvalidState,
                    $"Chamber '{Id}' cannot stop " +
                    $"from state '{runtime.State}'.");
                return false;
            }

            SetStateUnsafe(ChamberState.Stopping);

            failure = CommandResult.Success();
            return true;
        }
    }

    private void ChangeState(
        ChamberState newState,
        string? newFaultCode = null,
        string? newFaultMessage = null)
    {
        lock (syncRoot)
        {
            SetStateUnsafe(
                newState,
                newFaultCode,
                newFaultMessage);
        }
    }

    private void SetStateUnsafe(
        ChamberState newState,
        string? newFaultCode = null,
        string? newFaultMessage = null)
    {
        if (runtime.State == newState &&
            runtime.FaultCode == newFaultCode &&
            runtime.FaultMessage == newFaultMessage)
        {
            return;
        }

        runtime = runtime with
        {
            State = newState,
            StateChangedAt = DateTimeOffset.UtcNow,
            FaultCode = newFaultCode,
            FaultMessage = newFaultMessage,
            ActiveGasTargets = newState is ChamberState.Idle or ChamberState.Faulted or ChamberState.Offline
                ? ChamberRuntimeState.EmptyGasTargets : runtime.ActiveGasTargets,
            StopCommandsCompletedAt = newState == ChamberState.Stopping
                ? runtime.StopCommandsCompletedAt : null
        };
    }
}