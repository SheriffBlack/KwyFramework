using KwyPecvd.Device;
using KwyPecvd.Device.OnOffValve;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.VacuumPump;

namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 负责腔体真空硬件绑定和状态聚合。
/// 
/// 真空子系统状态机
/// </summary>
internal sealed class ChamberVacuumSystem
{
    private readonly string chamberId;
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);

    private ChamberVacuumState state = ChamberVacuumState.Offline;

    private DateTimeOffset stateChangedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? pumpDownStartedAt;

    private string? faultCode;

    private string? faultMessage;

    private readonly ChamberVacuumDefinition definition;

    /// <summary>
    /// 由整机 Registry 按 Definition 中的 ID 一次解析得到。
    /// 这些字典只是当前腔体的强类型视图，不是第二个 Registry。
    /// </summary>
    private readonly IReadOnlyDictionary<string, IPressureGauge> pressureGauges;

    private readonly IReadOnlyDictionary<string, IVacuumPump> pumps;

    private readonly IReadOnlyDictionary<string, IOnOffValve> valves;

    private readonly IPressureGauge pumpDownPressureGauge;

    public ChamberVacuumSystem(
        string chamberId,
        ChamberVacuumDefinition definition,
        IHardwareComponentRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chamberId);

        ArgumentNullException.ThrowIfNull(definition);

        ArgumentNullException.ThrowIfNull(registry);

        definition.Validate();

        this.chamberId = chamberId;
        this.definition = definition;

        pressureGauges = BindComponents<IPressureGauge>(
            definition.PressureGauges.Select(
                item => item.PressureGaugeId),
            registry,
            item => item.Definition.ModuleId,
            "Pressure gauge");

        pumps = BindComponents<IVacuumPump>(
            definition.PumpIds,
            registry,
            item => item.Definition.ModuleId,
            "Vacuum pump");

        valves = BindComponents<IOnOffValve>(
            definition.ValveIds,
            registry,
            item => item.Definition.ModuleId,
            "Vacuum valve");

        var pumpDownGaugeId = definition.PressureGauges
            .Single(item => item.Roles.HasFlag(
                ChamberPressureGaugeRole.PumpDown))
            .PressureGaugeId;

        pumpDownPressureGauge = pressureGauges[pumpDownGaugeId];
    }

    public ChamberVacuumSnapshot CreateSnapshot()
    {
        ChamberVacuumState currentState;
        DateTimeOffset currentStateChangedAt;
        string? currentFaultCode;
        string? currentFaultMessage;

        lock (syncRoot)
        {
            currentState = state;
            currentStateChangedAt = stateChangedAt;
            currentFaultCode = faultCode;
            currentFaultMessage = faultMessage;
        }

        return new ChamberVacuumSnapshot
        {
            State = currentState,

            PressureGauges = pressureGauges.Values
                .Select(item => item.Snapshot)
                .ToArray(),

            Pumps = pumps.Values
                .Select(item => item.Snapshot)
                .ToArray(),

            Valves = valves.Values
                .Select(item => item.Snapshot)
                .ToArray(),

            StateChangedAt = currentStateChangedAt,

            FaultCode = currentFaultCode,

            FaultMessage = currentFaultMessage,

            Timestamp = DateTimeOffset.UtcNow
        };
    }

    public bool TryInitialize(out CommandResult failure)
    {
        foreach (var pressureGauge in pressureGauges.Values)
        {
            var pressureSnapshot = pressureGauge.Snapshot;

            if (pressureSnapshot.IsOffline)
            {
                var message =
                    $"Pressure gauge " +
                    $"'{pressureGauge.Id}' is offline.";

                ChangeState(
                    ChamberVacuumState.Offline,
                    ResultCodes.Offline,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }

            if (!pressureSnapshot.IsReadingValid)
            {
                var message =
                    $"Pressure gauge " +
                    $"'{pressureGauge.Id}' reading is invalid. " +
                    $"Diagnostic: " +
                    $"{pressureSnapshot.DiagnosticCode ??
                       "UNKNOWN"}.";

                ChangeState(
                    ChamberVacuumState.Offline,
                    pressureSnapshot.DiagnosticCode ??
                    ResultCodes.NotReady,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }
        }

        foreach (var pump in pumps.Values)
        {
            var pumpSnapshot = pump.Snapshot;

            if (pumpSnapshot.IsOffline ||
                !pumpSnapshot.IsFeedbackValid)
            {
                var message =
                    $"Vacuum pump '{pump.Id}' is not ready.";

                ChangeState(
                    ChamberVacuumState.Offline,
                    ResultCodes.NotReady,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }

            if (pumpSnapshot.HasAlarm ||
                pumpSnapshot.IsOverloaded)
            {
                var message =
                    $"Vacuum pump '{pump.Id}' has a fault.";

                ChangeState(
                    ChamberVacuumState.Faulted,
                    pumpSnapshot.DiagnosticCode ??
                    ResultCodes.NotReady,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }
        }
        foreach (var valve in valves.Values)
        {
            var valveSnapshot = valve.Snapshot;

            if (!valveSnapshot.IsOffline &&
                valveSnapshot.IsFeedbackValid)
            {
                continue;
            }

            var message =
                $"Vacuum valve '{valve.Id}' is not ready.";

            ChangeState(
                ChamberVacuumState.Offline,
                ResultCodes.NotReady,
                message);

            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                message);

            return false;
        }
        ChangeState(ChamberVacuumState.Idle);

        failure = CommandResult.Success(
            $"Chamber '{chamberId}' vacuum " +
            "system initialized.");

        return true;
    }

    public async Task<CommandResult> StartPumpDownAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var currentState = GetState();

            if (currentState != ChamberVacuumState.Idle)
            {
                return CommandResult.Failure(
                    ResultCodes.InvalidState,
                    $"Chamber '{chamberId}' vacuum " +
                    $"system must be Idle before pump-down. " +
                    $"Current state: {currentState}.");
            }

            foreach (var pump in pumps.Values)
            {
                var pumpSnapshot = pump.Snapshot;

                if (pumpSnapshot.IsOffline ||
                    !pumpSnapshot.IsFeedbackValid ||
                    pumpSnapshot.HasAlarm ||
                    pumpSnapshot.IsOverloaded)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum pump '{pump.Id}' " +
                        "is not ready.");
                }
            }

            foreach (var valve in valves.Values)
            {
                var valveSnapshot = valve.Snapshot;

                if (valveSnapshot.IsOffline ||
                    !valveSnapshot.IsFeedbackValid)
                {
                    return CommandResult.Failure(
                        ResultCodes.NotReady,
                        $"Vacuum valve '{valve.Id}' " +
                        "is not ready.");
                }

                if (valveSnapshot.State !=
                    OnOffValveState.Closed)
                {
                    return CommandResult.Failure(
                        ResultCodes.InvalidState,
                        $"Vacuum valve '{valve.Id}' must be " +
                        $"closed before pump-down. Current " +
                        $"state: {valveSnapshot.State}.");
                }
            }

            var startedPumps = new List<IVacuumPump>();

            foreach (var pump in pumps.Values)
            {
                var result = await pump
                    .StartAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!result.Succeeded)
                {
                    await StopPumpsAsync(
                            startedPumps,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                    ChangeState(
                        ChamberVacuumState.Faulted,
                        result.Code,
                        result.Message);

                    return result;
                }

                startedPumps.Add(pump);
            }

            lock (syncRoot)
            {
                pumpDownStartedAt = DateTimeOffset.UtcNow;
            }

            ChangeState(ChamberVacuumState.StartingPump);

            return CommandResult.Success(
                $"Chamber '{chamberId}' vacuum pump " +
                "start commands accepted.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ChangeState(
                ChamberVacuumState.Faulted,
                ResultCodes.InternalError,
                exception.Message);

            return CommandResult.Failure(
                ResultCodes.InternalError,
                exception.Message);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var currentState = GetState();

            if (currentState is
                ChamberVacuumState.Offline or
                ChamberVacuumState.Idle or
                ChamberVacuumState.Faulted)
            {
                return;
            }

            var pressure = pumpDownPressureGauge.Snapshot;

            if (!TryValidateRuntimeFeedback(
                    pressure,
                    pumps.Values,
                    valves.Values,
                    out var faultCode,
                    out var faultMessage))
            {
                ChangeState(
                    ChamberVacuumState.Faulted,
                    faultCode,
                    faultMessage);

                return;
            }

            if (IsPumpDownTimedOut())
            {
                ChangeState(
                    ChamberVacuumState.Faulted,
                    ResultCodes.Timeout,
                    $"Chamber '{chamberId}' failed " +
                    $"to reach {definition.ReadyPressure} " +
                    $"{pressure.Unit} within " +
                    $"{definition.PumpDownTimeout}.");

                return;
            }

            switch (currentState)
            {
                case ChamberVacuumState.StartingPump:
                    await HandleStartingPumpAsync(
                            pumps.Values,
                            valves.Values,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case ChamberVacuumState.OpeningValves:
                    HandleOpeningValves(valves.Values);
                    break;

                case ChamberVacuumState.Pumping:
                    HandlePumping(
                        pressure,
                        valves.Values);
                    break;

                case ChamberVacuumState.Ready:
                    // Ready 阶段仍执行上面的硬件健康检查。
                    break;

                case ChamberVacuumState.Stopping:
                    // 下一阶段实现安全停止。
                    break;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ChangeState(
                ChamberVacuumState.Faulted,
                ResultCodes.InternalError,
                exception.Message);

            throw;
        }
        finally
        {
            operationGate.Release();
        }
    }

    private IReadOnlyDictionary<string, TComponent> BindComponents<TComponent>(
            IEnumerable<string> componentIds,
            IHardwareComponentRegistry registry,
            Func<TComponent, string> getModuleId,
            string componentName)
        where TComponent : class, IHardwareComponent
    {
        var bindings = new Dictionary<string, TComponent>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var componentId in componentIds)
        {
            var component =
                registry.GetRequired<TComponent>(componentId);

            var moduleId = getModuleId(component);

            if (!string.Equals(
                    moduleId,
                    chamberId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{componentName} '{component.Id}' belongs " +
                    $"to module '{moduleId}', not chamber " +
                    $"'{chamberId}'.");
            }

            bindings.Add(component.Id, component);
        }

        return bindings;
    }

    private ChamberVacuumState GetState()
    {
        lock (syncRoot)
        {
            return state;
        }
    }
    private DateTimeOffset? GetPumpDownStartedAt()
    {
        lock (syncRoot)
        {
            return pumpDownStartedAt;
        }
    }

    private void ClearPumpDownStartedAt()
    {
        lock (syncRoot)
        {
            pumpDownStartedAt = null;
        }
    }

    private void ChangeState(
        ChamberVacuumState newState,
        string? newFaultCode = null,
        string? newFaultMessage = null)
    {
        lock (syncRoot)
        {
            if (state != newState)
            {
                stateChangedAt =
                    DateTimeOffset.UtcNow;
            }

            state = newState;
            faultCode = newFaultCode;
            faultMessage = newFaultMessage;
        }
    }

    private static bool TryValidateRuntimeFeedback(
        PressureGaugeSnapshot pressure,
        IEnumerable<IVacuumPump> pumps,
        IEnumerable<IOnOffValve> valves,
        out string faultCode,
        out string faultMessage)
    {
        if (pressure.IsOffline ||
            !pressure.IsReadingValid)
        {
            faultCode =
                pressure.DiagnosticCode ??
                ResultCodes.NotReady;

            faultMessage =
                $"Pressure gauge '{pressure.Id}' " +
                "feedback is unavailable.";

            return false;
        }

        foreach (var pump in pumps)
        {
            var snapshot = pump.Snapshot;

            if (snapshot.IsOffline ||
                !snapshot.IsFeedbackValid)
            {
                faultCode =
                    snapshot.DiagnosticCode ??
                    ResultCodes.NotReady;

                faultMessage =
                    $"Vacuum pump '{pump.Id}' feedback " +
                    "is unavailable.";

                return false;
            }

            if (snapshot.HasAlarm ||
                snapshot.IsOverloaded)
            {
                faultCode =
                    snapshot.DiagnosticCode ??
                    ResultCodes.NotReady;

                faultMessage =
                    $"Vacuum pump '{pump.Id}' has a fault.";

                return false;
            }

            if (!snapshot.IsN2Ready ||
                !snapshot.IsExhaustReady)
            {
                faultCode =
                    snapshot.DiagnosticCode ??
                    ResultCodes.NotReady;

                faultMessage =
                    $"Vacuum pump '{pump.Id}' utilities " +
                    "are not ready.";

                return false;
            }
        }

        foreach (var valve in valves)
        {
            var snapshot = valve.Snapshot;

            if (!snapshot.IsOffline &&
                snapshot.IsFeedbackValid &&
                snapshot.State != OnOffValveState.Fault &&
                !snapshot.IsMotionTimedOut)
            {
                continue;
            }

            faultCode =
                snapshot.DiagnosticCode ??
                ResultCodes.NotReady;

            faultMessage =
                $"Vacuum valve '{valve.Id}' " +
                "feedback is invalid.";

            return false;
        }

        faultCode = string.Empty;
        faultMessage = string.Empty;
        return true;
    }

    private async Task HandleStartingPumpAsync(
        IEnumerable<IVacuumPump> pumps,
        IEnumerable<IOnOffValve> valves,
        CancellationToken cancellationToken)
    {
        if (!pumps.All(
                pump => pump.Snapshot.IsRunning))
        {
            return;
        }

        var openedValves = new List<IOnOffValve>();

        foreach (var valve in valves)
        {
            var result = await valve
                .OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                await CloseValvesAsync(
                        openedValves,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                await StopPumpsAsync(
                        pumps,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                ChangeState(
                    ChamberVacuumState.Faulted,
                    result.Code,
                    result.Message);

                return;
            }

            openedValves.Add(valve);
        }

        ChangeState(
            ChamberVacuumState.OpeningValves);
    }

    private void HandleOpeningValves(
        IEnumerable<IOnOffValve> valves)
    {
        if (!valves.All(
                valve => valve.Snapshot.State ==
                    OnOffValveState.Open))
        {
            return;
        }

        ChangeState(
            ChamberVacuumState.Pumping);
    }

    private void HandlePumping(
        PressureGaugeSnapshot pressure,
        IEnumerable<IOnOffValve> valves)
    {
        var closedValve = valves.FirstOrDefault(
            valve => valve.Snapshot.State !=
                OnOffValveState.Open);

        if (closedValve is not null)
        {
            ChangeState(
                ChamberVacuumState.Faulted,
                ResultCodes.InvalidState,
                $"Vacuum valve '{closedValve.Id}' " +
                "is not open during pump-down.");

            return;
        }

        if (pressure.Pressure >
            definition.ReadyPressure)
        {
            return;
        }

        ClearPumpDownStartedAt();

        ChangeState(
            ChamberVacuumState.Ready);
    }

    private static async Task StopPumpsAsync(
        IEnumerable<IVacuumPump> pumps,
        CancellationToken cancellationToken)
    {
        foreach (var pump in pumps.Reverse())
        {
            try
            {
                await pump.StopAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // 保留原始启动故障；停止失败由后续状态诊断处理。
            }
        }
    }

    private static async Task CloseValvesAsync(
        IEnumerable<IOnOffValve> valves,
        CancellationToken cancellationToken)
    {
        foreach (var valve in valves.Reverse())
        {
            try
            {
                await valve.CloseAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // 保留原始开阀故障；关闭失败由后续状态诊断处理。
            }
        }
    }

    private bool IsPumpDownTimedOut()
    {
        var startedAt =
            GetPumpDownStartedAt();

        if (startedAt is null)
        {
            return false;
        }

        return DateTimeOffset.UtcNow -
            startedAt.Value >=
            definition.PumpDownTimeout;
    }
}
