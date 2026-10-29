using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Process.Chamber.Gas;
using KwyPecvd.Process.Chamber.Interlock;
using KwyPecvd.Process.Chamber.Vacuum;

namespace KwyPecvd.Process.Chamber;

/// <summary>
/// 负责腔体状态和高层操作编排。
/// 
/// 整个 PM 的业务状态机
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

    public Task<CommandResult> StartPumpDownAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(() => StartPumpDownCoreAsync(cancellationToken),cancellationToken);

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
            if (!gasSystem.TryInitialize(out failure))
            {
                ChangeState(ChamberState.Offline);
                return Task.FromResult(failure);
            }

            if (!vacuumSystem.TryInitialize(out failure))
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
        if (!gasSystem.TryGetMfc(
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
        if (!gasSystem.TryGetMfc(
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

    private async Task<CommandResult> StartPumpDownCoreAsync(CancellationToken cancellationToken)
    {
        var current = GetRuntimeState();

        if (current.State != ChamberState.Idle)
        {
            return CommandResult.Failure(
                ResultCodes.InvalidState,
                $"Chamber '{Id}' must be Idle before pump-down.");
        }

        var result =
            await vacuumSystem.StartPumpDownAsync(
                cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        BeginVacuumPreparation();

        return result;
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

        if (!gasSystem.TryInitialize(
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
            await vacuumSystem.ExecuteCycleAsync(cancellationToken);

            var vacuum = vacuumSystem.CreateSnapshot();

            if (vacuum.State == ChamberVacuumState.Faulted)
            {
                TryTransition(
                    ChamberState.Preparing,
                    ChamberState.Faulted,
                    vacuum.FaultCode,
                    vacuum.FaultMessage);

                return;
            }

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

    /// <summary>
    /// 状态机 Preparing → Processing
    /// </summary>
    /// <param name="current"></param>
    private void EvaluatePreparation(ChamberRuntimeState current)
    {
        var preparation = current.Preparation;

        // preparation is null表示腔体进入了 Preparing，因为仅在 Preparing 状态中存在。
        if (preparation is null ||
            preparation.Requirements ==
                ChamberPreparationRequirement.None)
        {
            // 只有当前状态仍然是 Preparing，才允许切换到 Faulted。
            TryTransition(
                ChamberState.Preparing,
                ChamberState.Faulted,
                ChamberFaultCodes.InvalidPreparationContext,
                "Chamber preparation context is missing.");
            return;
        }

        if (preparation.Requirements.HasFlag(
                ChamberPreparationRequirement.Vacuum))
        {
            var vacuum = vacuumSystem.CreateSnapshot();

            if (vacuum.State == ChamberVacuumState.Faulted)
            {
                TryTransition(
                    ChamberState.Preparing,
                    ChamberState.Faulted,
                    vacuum.FaultCode,
                    vacuum.FaultMessage);
                return;
            }

            if (vacuum.State != ChamberVacuumState.Ready)
            {
                return;
            }
        }

        if (preparation.Requirements.HasFlag(
                ChamberPreparationRequirement.Gas))
        {
            var gas = gasSystem.EvaluatePreparation(
                preparation.GasTargets);

            if (gas.Status == GasPreparationStatus.Faulted)
            {
                TryTransition(
                    ChamberState.Preparing,
                    ChamberState.Faulted,
                    gas.FaultCode,
                    gas.FaultMessage);
                return;
            }

            if (gas.Status != GasPreparationStatus.Ready)
            {
                return;
            }
        }

        TryTransition(
            ChamberState.Preparing,
            ChamberState.Processing);
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

    /// <summary>
    /// 只有状态机仍处于我预期的旧状态时，才允许切换到新状态。
    /// </summary>
    /// <param name="expectedState">期望的当前状态</param>
    /// <param name="newState">准备进入的新状态</param>
    /// <param name="faultCode">可选故障码</param>
    /// <param name="faultMessage">可选故障描述</param>
    /// <returns></returns>
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

            runtime = runtime with { Preparation = null };

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
                Preparation = new ChamberPreparationContext
                {
                    Requirements =
                        ChamberPreparationRequirement.Gas,
                    GasTargets = targets
                }
            };

            SetStateUnsafe(ChamberState.Preparing);
            failure = CommandResult.Success();
            return true;
        }
    }

    private void BeginVacuumPreparation()
    {
        lock (syncRoot)
        {
            runtime = runtime with
            {
                Preparation = new ChamberPreparationContext
                {
                    Requirements =
                        ChamberPreparationRequirement.Vacuum
                }
            };

            SetStateUnsafe(ChamberState.Preparing);
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

    /// <summary>
    /// 这个方法自己不加锁，调用者必须已经持有 syncRoot 锁。
    /// </summary>
    /// <param name="newState"></param>
    /// <param name="newFaultCode"></param>
    /// <param name="newFaultMessage"></param>
    private void SetStateUnsafe(
        ChamberState newState,
        string? newFaultCode = null,
        string? newFaultMessage = null)
    {
        // 如果状态没有变化，就不更新 StateChangedAt 时间戳。防止每个 100ms 周期都刷新 StateChangedAt。
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
            Preparation = newState == ChamberState.Preparing        // Preparing → 保留本轮准备任务
                ? runtime.Preparation                               // Processing/Idle/Faulted → 清空准备任务
                : null,
            StopCommandsCompletedAt = newState == ChamberState.Stopping
                ? runtime.StopCommandsCompletedAt : null
        };
    }
}
