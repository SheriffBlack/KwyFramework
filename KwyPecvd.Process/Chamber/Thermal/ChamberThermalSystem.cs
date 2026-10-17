using KwyPecvd.Device;
using KwyPecvd.Device.Thermal.TemperatureController;
using KwyPecvd.Device.Thermal.TemperatureSensor;

namespace KwyPecvd.Process.Chamber.Thermal;

/*
 
 StartHeatingAsync
    → TryCreateHeatingPlan
       ├─ ZoneId存在
       ├─ 没有重复Zone
       ├─ 目标在控制器范围内
       └─ 控制器和传感器健康
    → 设置所有温区目标
    → 使能所有温控器
    → 保存ActiveTargets
    → 记录HeatingStartedAt
    → State = Heating
 
 */

/// <summary>
/// 负责腔体温区的硬件绑定、状态聚合和温度准备。
/// </summary>
internal sealed class ChamberThermalSystem
{
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly string chamberId;

    private static readonly IReadOnlyDictionary<string, ChamberTemperatureTarget> EmptyTargets =new Dictionary< string, ChamberTemperatureTarget>(
            StringComparer.OrdinalIgnoreCase);

    private readonly ChamberThermalDefinition definition;

    private DateTimeOffset? heatingStartedAt;

    /// <summary>
    /// 构造时从整机 Registry 一次解析得到的
    /// 当前腔体温区强类型视图。
    /// </summary>
    private readonly IReadOnlyDictionary<string, ChamberThermalZoneBinding> zones;

    private ChamberThermalState state = ChamberThermalState.Offline;

    private IReadOnlyDictionary<string, ChamberTemperatureTarget> activeTargets = EmptyTargets;

    private DateTimeOffset? stableSince;

    private string? faultCode;

    private string? faultMessage;

    public ChamberThermalSystem(
        string chamberId,
        ChamberThermalDefinition definition,
        IHardwareComponentRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chamberId);

        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registry);

        definition.Validate();

        this.chamberId = chamberId;
        this.definition = definition;

        zones = definition.Zones
            .Select(zone => BindZone(
                zone,
                registry))
            .ToDictionary(
                binding => binding.Definition.ZoneId,
                StringComparer.OrdinalIgnoreCase);
    }

    private ChamberThermalZoneBinding BindZone(
        ChamberThermalZoneDefinition zone,
        IHardwareComponentRegistry registry)
    {
        var controller =
            registry.GetRequired<
                ITemperatureController>(
                zone.ControllerId);

        ValidateModuleBinding(
            controller.Id,
            controller.Definition.ModuleId,
            "Temperature controller");

        var sensors =
            zone.MonitoringSensorIds
                .Select(sensorId =>
                    registry.GetRequired<
                        ITemperatureSensor>(
                        sensorId))
                .ToArray();

        foreach (var sensor in sensors)
        {
            ValidateModuleBinding(
                sensor.Id,
                sensor.Definition.ModuleId,
                "Temperature sensor");

            if (!string.Equals(
                    sensor.Definition.Unit,
                    controller.Definition.Unit,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Temperature sensor '{sensor.Id}' " +
                    $"uses unit '{sensor.Definition.Unit}', " +
                    $"but controller '{controller.Id}' uses " +
                    $"'{controller.Definition.Unit}'.");
            }
        }

        return new ChamberThermalZoneBinding(
            zone,
            controller,
            sensors);
    }

    public async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ChamberThermalState currentState;
            IReadOnlyDictionary<string,ChamberTemperatureTarget> targets;
            DateTimeOffset? startedAt;

            lock (syncRoot)
            {
                currentState = state;
                targets = activeTargets;
                startedAt = heatingStartedAt;
            }

            if (currentState is
                ChamberThermalState.Offline or
                ChamberThermalState.Idle or
                ChamberThermalState.Faulted)
            {
                return;
            }

            if (currentState == ChamberThermalState.Stopping)
            {
                // 停止闭环下一步实现。
                return;
            }

            if (targets.Count == 0)
            {
                await EnterFaultAsync(
                        ResultCodes.InvalidState,
                        "Thermal system has no active " +
                        "temperature targets.")
                    .ConfigureAwait(false);

                return;
            }

            /*
             * 先检查参与本次温度准备的所有温区，
             * 同时计算它们是否全部进入允许范围。
             */
            var allWithinTolerance = true;

            foreach (var target in targets.Values)
            {
                var zone = zones[target.ZoneId];

                if (!TryValidateRuntimeFeedback(
                        zone,
                        out var runtimeFaultCode,
                        out var runtimeFaultMessage))
                {
                    await EnterFaultAsync(
                            runtimeFaultCode,
                            runtimeFaultMessage)
                        .ConfigureAwait(false);

                    return;
                }

                var controllerSnapshot = zone.Controller.Snapshot;

                var withinTolerance =
                    Math.Abs(
                        controllerSnapshot.Feedback -
                        target.Temperature) <=
                    zone.Controller.Definition.Tolerance;

                if (!withinTolerance)
                {
                    allWithinTolerance = false;
                }
            }

            var now = DateTimeOffset.UtcNow;

            if (!allWithinTolerance)
            {
                HandleOutsideTolerance(currentState, now);

                if (IsHeatingTimedOut(now))
                {
                    await EnterFaultAsync(
                            ResultCodes.Timeout,
                            $"Chamber '{chamberId}' failed " +
                            "to reach the requested temperature " +
                            $"within {definition.HeatingTimeout}.")
                        .ConfigureAwait(false);
                }

                return;
            }

            HandleInsideTolerance(now);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await EnterFaultAsync(
                    ResultCodes.InternalError,
                    exception.Message)
                .ConfigureAwait(false);

            throw;
        }
        finally
        {
            operationGate.Release();
        }
    }

    private static bool TryValidateRuntimeFeedback(
        ChamberThermalZoneBinding zone,
        out string faultCode,
        out string faultMessage)
    {
        var controllerSnapshot = zone.Controller.Snapshot;

        if (controllerSnapshot.IsOffline)
        {
            faultCode = controllerSnapshot.DiagnosticCode ?? ResultCodes.Offline;

            faultMessage =
                $"Temperature controller " +
                $"'{zone.Controller.Id}' is offline.";

            return false;
        }

        if (!controllerSnapshot.IsFeedbackValid ||
            !double.IsFinite(controllerSnapshot.Feedback))
        {
            faultCode = controllerSnapshot.DiagnosticCode ?? ResultCodes.NotReady;

            faultMessage =
                $"Temperature controller " +
                $"'{zone.Controller.Id}' feedback is invalid.";

            return false;
        }

        if (!controllerSnapshot.CommandedEnabled)
        {
            faultCode = ResultCodes.InvalidState;

            faultMessage =
                $"Temperature controller " +
                $"'{zone.Controller.Id}' is not enabled.";

            return false;
        }

        foreach (var sensor in zone.MonitoringSensors)
        {
            var sensorSnapshot = sensor.Snapshot;

            if (sensorSnapshot.IsOffline)
            {
                faultCode = sensorSnapshot.DiagnosticCode ?? ResultCodes.Offline;

                faultMessage =
                    $"Temperature sensor " +
                    $"'{sensor.Id}' is offline.";

                return false;
            }

            if (!sensorSnapshot.IsReadingValid ||
                !double.IsFinite(
                    sensorSnapshot.Temperature))
            {
                faultCode =
                    sensorSnapshot.DiagnosticCode ??
                    ResultCodes.NotReady;

                faultMessage =
                    $"Temperature sensor " +
                    $"'{sensor.Id}' reading is invalid.";

                return false;
            }
        }

        faultCode = string.Empty;
        faultMessage = string.Empty;

        return true;
    }

    private void HandleOutsideTolerance(
        ChamberThermalState previousState,
        DateTimeOffset now)
    {
        lock (syncRoot)
        {
            stableSince = null;

            /*
             * Ready之后温度重新偏离，
             * 开始一轮新的恢复超时计时。
             */
            if (previousState ==ChamberThermalState.Ready ||
                heatingStartedAt is null)
            {
                heatingStartedAt = now;
            }
        }

        ChangeState(ChamberThermalState.Heating);
    }

    private void HandleInsideTolerance(DateTimeOffset now)
    {
        DateTimeOffset currentStableSince;

        lock (syncRoot)
        {
            stableSince ??= now;
            currentStableSince = stableSince.Value;
        }

        if (now - currentStableSince < definition.StableDuration)
        {
            ChangeState(ChamberThermalState.Stabilizing);

            return;
        }

        lock (syncRoot)
        {
            heatingStartedAt = null;
        }

        ChangeState(ChamberThermalState.Ready);
    }

    private bool IsHeatingTimedOut(DateTimeOffset now)
    {
        DateTimeOffset? startedAt;

        lock (syncRoot)
        {
            startedAt = heatingStartedAt;
        }

        return startedAt is not null &&
            now - startedAt.Value >= definition.HeatingTimeout;
    }

    private async Task EnterFaultAsync(string code, string message)
    {
        IReadOnlyList<ITemperatureController> controllers;

        lock (syncRoot)
        {
            controllers = activeTargets.Keys
                .Where(zones.ContainsKey)
                .Select(
                    zoneId =>
                        zones[zoneId].Controller)
                .ToArray();

            heatingStartedAt = null;
            stableSince = null;
        }

        await DisableControllersAsync(
                controllers,
                CancellationToken.None)
            .ConfigureAwait(false);

        ChangeState(
            ChamberThermalState.Faulted,
            code,
            message);
    }

    private void ValidateModuleBinding(
        string componentId,
        string moduleId,
        string componentName)
    {
        if (string.Equals(
                moduleId,
                chamberId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{componentName} '{componentId}' belongs " +
            $"to module '{moduleId}', not chamber " +
            $"'{chamberId}'.");
    }

    public ChamberThermalSnapshot CreateSnapshot()
    {
        ChamberThermalState currentState;

        IReadOnlyDictionary<string, ChamberTemperatureTarget> currentTargets;

        DateTimeOffset? currentStableSince;
        string? currentFaultCode;
        string? currentFaultMessage;

        lock (syncRoot)
        {
            currentState = state;
            currentTargets = activeTargets;
            currentStableSince = stableSince;
            currentFaultCode = faultCode;
            currentFaultMessage = faultMessage;
        }

        return new ChamberThermalSnapshot
        {
            State = currentState,

            Zones = zones.Values
                .Select(zone => CreateZoneSnapshot(
                    zone,
                    currentTargets,
                    currentState))
                .ToArray(),

            StableSince = currentStableSince,

            FaultCode = currentFaultCode,

            FaultMessage = currentFaultMessage,

            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static ChamberThermalZoneSnapshot CreateZoneSnapshot(
        ChamberThermalZoneBinding zone,
        IReadOnlyDictionary<string, ChamberTemperatureTarget> targets,
        ChamberThermalState thermalState)
    {
        targets.TryGetValue(
            zone.Definition.ZoneId,
            out var target);

        var controllerSnapshot =
            zone.Controller.Snapshot;

        var withinTolerance =
            target is not null &&
            !controllerSnapshot.IsOffline &&
            controllerSnapshot.IsFeedbackValid &&
            double.IsFinite(
                controllerSnapshot.Feedback) &&
            Math.Abs(
                controllerSnapshot.Feedback -
                target.Temperature) <=
            zone.Controller.Definition.Tolerance;

        return new ChamberThermalZoneSnapshot
        {
            ZoneId = zone.Definition.ZoneId,

            DisplayName = zone.Definition.DisplayName,

            TargetTemperature = target?.Temperature,

            IsWithinTolerance = withinTolerance,

            IsReady =
                thermalState ==
                    ChamberThermalState.Ready &&
                withinTolerance,

            Controller = controllerSnapshot,

            MonitoringSensors =
                zone.MonitoringSensors
                    .Select(sensor => sensor.Snapshot)
                    .ToArray()
        };
    }

    public async Task<CommandResult> StartHeatingAsync(
        IReadOnlyCollection<ChamberTemperatureTarget> targets,
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateHeatingPlan(
                targets,
                out var plan,
                out var failure))
        {
            return failure;
        }

        await operationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var enabledControllers =
            new List<ITemperatureController>();

        try
        {
            var currentState = GetState();

            if (currentState != ChamberThermalState.Idle)
            {
                return CommandResult.Failure(
                    ResultCodes.InvalidState,
                    $"Chamber '{chamberId}' thermal " +
                    $"system must be Idle before heating. " +
                    $"Current state: {currentState}.");
            }

            /*
             * 第一阶段：为所有温区设置目标。
             * 这时暂时不使能输出。
             */
            foreach (var command in plan.Commands)
            {
                var result =
                    await command.Zone.Controller
                        .SetTargetTemperatureAsync(
                            command.Target.Temperature,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!result.Succeeded)
                {
                    ChangeState(
                        ChamberThermalState.Faulted,
                        result.Code,
                        result.Message);

                    return result;
                }
            }

            /*
             * 第二阶段：所有目标设置成功以后，
             * 再依次使能温控器。
             */
            foreach (var command in plan.Commands)
            {
                var controller = command.Zone.Controller;

                var result =
                    await controller
                        .EnableAsync(cancellationToken)
                        .ConfigureAwait(false);

                if (!result.Succeeded)
                {
                    await DisableControllersAsync(
                            enabledControllers,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                    ChangeState(
                        ChamberThermalState.Faulted,
                        result.Code,
                        result.Message);

                    return result;
                }

                enabledControllers.Add(controller);
            }

            var now = DateTimeOffset.UtcNow;

            lock (syncRoot)
            {
                activeTargets = plan.Targets;
                heatingStartedAt = now;
                stableSince = null;
            }

            ChangeState(ChamberThermalState.Heating);

            return CommandResult.Success(
                $"Chamber '{chamberId}' thermal " +
                "heating commands accepted.");
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            await DisableControllersAsync(
                    enabledControllers,
                    CancellationToken.None)
                .ConfigureAwait(false);

            throw;
        }
        catch (Exception exception)
        {
            await DisableControllersAsync(
                    enabledControllers,
                    CancellationToken.None)
                .ConfigureAwait(false);

            ChangeState(
                ChamberThermalState.Faulted,
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

    public bool TryInitialize(out CommandResult failure)
    {
        foreach (var zone in zones.Values)
        {
            var controller = zone.Controller;
            var controllerSnapshot = controller.Snapshot;

            if (controllerSnapshot.IsOffline ||
                !controllerSnapshot.IsFeedbackValid ||
                !double.IsFinite(
                    controllerSnapshot.Feedback))
            {
                var message =
                    $"Temperature controller " +
                    $"'{controller.Id}' is not ready. " +
                    $"Diagnostic: " +
                    $"{controllerSnapshot.DiagnosticCode ??
                       "UNKNOWN"}.";

                ChangeState(
                    ChamberThermalState.Offline,
                    controllerSnapshot.DiagnosticCode ??
                    ResultCodes.NotReady,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }

            foreach (var sensor in zone.MonitoringSensors)
            {
                var sensorSnapshot = sensor.Snapshot;

                if (!sensorSnapshot.IsOffline &&
                    sensorSnapshot.IsReadingValid &&
                    double.IsFinite(
                        sensorSnapshot.Temperature))
                {
                    continue;
                }

                var message =
                    $"Temperature sensor " +
                    $"'{sensor.Id}' is not ready. " +
                    $"Diagnostic: " +
                    $"{sensorSnapshot.DiagnosticCode ??
                       "UNKNOWN"}.";

                ChangeState(
                    ChamberThermalState.Offline,
                    sensorSnapshot.DiagnosticCode ??
                    ResultCodes.NotReady,
                    message);

                failure = CommandResult.Failure(
                    ResultCodes.NotReady,
                    message);

                return false;
            }
        }

        lock (syncRoot)
        {
            activeTargets = EmptyTargets;
            stableSince = null;
        }

        ChangeState(ChamberThermalState.Idle);

        failure = CommandResult.Success(
            $"Chamber '{chamberId}' thermal " +
            "system initialized.");

        return true;
    }


    public bool TryCreateHeatingPlan(
        IReadOnlyCollection<ChamberTemperatureTarget> targets,
        out ChamberHeatingPlan plan,
        out CommandResult failure)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Count == 0)
        {
            plan = default!;

            failure = CommandResult.Failure(
                ResultCodes.InvalidArgument,
                "At least one temperature target " +
                "is required.");

            return false;
        }

        var targetMap = new Dictionary<string, ChamberTemperatureTarget>(StringComparer.OrdinalIgnoreCase);

        var commands = new List<ChamberHeatingCommand>(targets.Count);

        foreach (var target in targets)
        {
            if (target is null)
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Temperature target cannot be null.");

                return false;
            }

            if (string.IsNullOrWhiteSpace(target.ZoneId))
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Thermal zone id is required.");

                return false;
            }

            if (!double.IsFinite(target.Temperature))
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Thermal zone '{target.ZoneId}' " +
                    "target temperature must be finite.");

                return false;
            }

            if (!zones.TryGetValue(target.ZoneId, out var zone))
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Thermal zone '{target.ZoneId}' " +
                    $"is not configured for chamber " +
                    $"'{chamberId}'.");

                return false;
            }

            if (!targetMap.TryAdd(target.ZoneId, target))
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Thermal zone '{target.ZoneId}' " +
                    "was specified more than once.");

                return false;
            }

            var controllerDefinition = zone.Controller.Definition;

            if (target.Temperature <
                    controllerDefinition.MinimumSetPoint ||
                target.Temperature >
                    controllerDefinition.MaximumSetPoint)
            {
                plan = default!;

                failure = CommandResult.Failure(
                    ResultCodes.OutOfRange,
                    $"Thermal zone '{target.ZoneId}' " +
                    $"target must be between " +
                    $"{controllerDefinition.MinimumSetPoint} " +
                    $"and " +
                    $"{controllerDefinition.MaximumSetPoint} " +
                    $"{controllerDefinition.Unit}.");

                return false;
            }

            if (!TryValidateZoneReady(
                    zone,
                    out failure))
            {
                plan = default!;
                return false;
            }

            commands.Add(
                new ChamberHeatingCommand(
                    zone,
                    target));
        }

        plan = new ChamberHeatingPlan(
            targetMap,
            commands);

        failure = CommandResult.Success();

        return true;
    }

    private static bool TryValidateZoneReady(
        ChamberThermalZoneBinding zone,
        out CommandResult failure)
    {
        var controllerSnapshot = zone.Controller.Snapshot;

        if (controllerSnapshot.IsOffline ||
            !controllerSnapshot.IsFeedbackValid ||
            !double.IsFinite(
                controllerSnapshot.Feedback))
        {
            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                $"Temperature controller " +
                $"'{zone.Controller.Id}' is not ready.");

            return false;
        }

        foreach (var sensor in zone.MonitoringSensors)
        {
            var sensorSnapshot = sensor.Snapshot;

            if (!sensorSnapshot.IsOffline &&
                sensorSnapshot.IsReadingValid &&
                double.IsFinite(
                    sensorSnapshot.Temperature))
            {
                continue;
            }

            failure = CommandResult.Failure(
                ResultCodes.NotReady,
                $"Temperature sensor " +
                $"'{sensor.Id}' is not ready.");

            return false;
        }

        failure = CommandResult.Success();
        return true;
    }

    private void ChangeState(
        ChamberThermalState newState,
        string? newFaultCode = null,
        string? newFaultMessage = null)
    {
        lock (syncRoot)
        {
            state = newState;
            faultCode = newFaultCode;
            faultMessage = newFaultMessage;

            if (newState is not
                ChamberThermalState.Stabilizing and not
                ChamberThermalState.Ready)
            {
                stableSince = null;
            }
        }
    }

    private ChamberThermalState GetState()
    {
        lock (syncRoot)
        {
            return state;
        }
    }

    private static async Task
        DisableControllersAsync(
            IEnumerable<ITemperatureController>
                controllers,
            CancellationToken cancellationToken)
    {
        foreach (var controller in
                 controllers.Reverse())
        {
            try
            {
                await controller
                    .DisableAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // 保留原始启动故障；
                // 关闭失败由后续周期状态诊断处理。
            }
        }
    }

    internal sealed record ChamberThermalZoneBinding(
        ChamberThermalZoneDefinition Definition,
        ITemperatureController Controller,
        IReadOnlyList<ITemperatureSensor> MonitoringSensors);

    /// <summary>所有目标验证完成后的完整计划 </summary>
    internal sealed record ChamberHeatingPlan(
        IReadOnlyDictionary<string, ChamberTemperatureTarget> Targets,
        IReadOnlyList<ChamberHeatingCommand> Commands);

    /// <summary>已经找到具体温控器的强类型命令 </summary>
    internal sealed record ChamberHeatingCommand(
        ChamberThermalZoneBinding Zone,
        ChamberTemperatureTarget Target);

}