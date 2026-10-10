using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;

namespace KwyPecvd.Process.Chambers;

/// <summary>负责腔体状态和高层操作编排。</summary>
public sealed class ChamberController : IChamberController
{
    private readonly object syncRoot = new();
    private readonly ChamberGasSystem gasSystem;
    private ChamberRuntimeState runtime = new();

    public ChamberController(
        ChamberDefinition definition,
        IHardwareComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);

        definition.Validate();
        Definition = definition;
        gasSystem = new ChamberGasSystem(
            definition.Id,
            definition.GasLines,
            registry);
    }

    public string Id => Definition.Id;

    public ChamberDefinition Definition { get; }

    public ChamberSnapshot Snapshot
    {
        get
        {
            ChamberRuntimeState current;

            lock (syncRoot)
            {
                current = runtime;
            }

            return new ChamberSnapshot
            {
                ChamberId = Id,
                State = current.State,
                StateChangedAt = current.StateChangedAt,
                FaultCode = current.FaultCode,
                FaultMessage = current.FaultMessage,
                GasFlows = gasSystem.CreateSnapshots(),
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    public Task<CommandResult> InitializeAsync(
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

            ChangeState(ChamberState.Idle);

            return Task.FromResult(
                CommandResult.Success(
                    $"Chamber '{Id}' initialized."));
        }
        catch (Exception exception)
        {
            ChangeState(
                ChamberState.Faulted,
                "CHAMBER_INITIALIZE_FAILED",
                exception.Message);

            return Task.FromResult(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    exception.Message));
        }
    }

    public Task<CommandResult> SetGasFlowAsync(
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

    public Task<CommandResult> StopGasFlowAsync(
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

    public async Task<CommandResult> StartGasPreparationAsync(
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

                    EndGasPreparation(
                        ChamberState.Faulted,
                        "GAS_PREPARATION_FAILED",
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
                EndGasPreparation(ChamberState.Idle);
            }
            else
            {
                EndGasPreparation(
                    ChamberState.Faulted,
                    "GAS_PREPARATION_CANCEL_FAILED",
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

            EndGasPreparation(
                ChamberState.Faulted,
                "GAS_PREPARATION_FAILED",
                message);

            return CommandResult.Failure(
                ResultCodes.NotReady,
                message);
        }

        return CommandResult.Success(
            $"Chamber '{Id}' started gas preparation.");
    }

    public Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var current = GetRuntimeState();

        if (current.State != ChamberState.Preparing)
        {
            return Task.CompletedTask;
        }

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

        return Task.CompletedTask;
    }

    private ChamberRuntimeState GetRuntimeState()
    {
        lock (syncRoot)
        {
            return runtime;
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

            try
            {
                var result = await mfc.StopAsync(
                    CancellationToken.None);

                if (!result.Succeeded)
                {
                    (failures ??= []).Add(
                        $"{mfc.Id}: {result.Message}");
                }
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(
                    $"{mfc.Id}: {exception.Message}");
            }
        }

        return failures is null
            ? null
            : string.Join("; ", failures);
    }

    private void EndGasPreparation(
        ChamberState newState,
        string? faultCode = null,
        string? faultMessage = null)
    {
        lock (syncRoot)
        {
            runtime = runtime with
            {
                ActiveGasTargets =
                    ChamberRuntimeState.EmptyGasTargets
            };

            SetStateUnsafe(
                newState,
                faultCode,
                faultMessage);
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
            FaultMessage = newFaultMessage
        };
    }
}
