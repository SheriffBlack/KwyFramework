using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Simulated;
using KwyPecvd.Device.OnOffValve;
using KwyPecvd.Device.OnOffValve.Simulated;
using KwyPecvd.Device.PressureGauge;
using KwyPecvd.Device.PressureGauge.Simulated;
using KwyPecvd.Device.RfMatcher;
using KwyPecvd.Device.RfMatcher.Simulated;
using KwyPecvd.Device.RfPowerSupply;
using KwyPecvd.Device.RfPowerSupply.Simulated;
using KwyPecvd.Device.Thermal.Simulated;
using KwyPecvd.Device.Thermal.TemperatureController;
using KwyPecvd.Device.Thermal.TemperatureController.Simulated;
using KwyPecvd.Device.Thermal.TemperatureSensor;
using KwyPecvd.Device.Thermal.TemperatureSensor.Simulated;
using KwyPecvd.Device.VacuumPump;
using KwyPecvd.Device.VacuumPump.Simulated;
using KwyPecvd.Process.Chambers;
using KwyPecvd.Process.Equipment;
using KwyPecvd.RT.Requests;
using KwyPecvd.RT.Runtime;
using KwyPecvd.RT.Simulated;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(
    options =>
    {
        options.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

//
// 1. IoC只注册一个硬件组件Registry、设备模块Registry
// IHardwareComponentRegistry：管理MFC、阀门、泵等硬件组件
// IEquipmentModuleRegistry：管理PM、TM、Load Lock等模块Controller
//
builder.Services.AddSingleton<
    IHardwareComponentRegistry,
    HardwareComponentRegistry>();

builder.Services.AddSingleton<
    IEquipmentModuleRegistry,
    EquipmentModuleRegistry>();

//
// 2. 注册RT后台周期服务
//
builder.Services.AddHostedService<EquipmentRuntimeService>();

var app = builder.Build();

//
// 3. 获取IoC管理的Registry单例
//
var hardwareRegistry = app.Services
    .GetRequiredService<
        IHardwareComponentRegistry>();

var moduleRegistry = app.Services
    .GetRequiredService<
        IEquipmentModuleRegistry>();

//
// 4. 创建SiH4 MFC
//
var sih4Mfc = new SimulatedMfc(
    new MfcDefinition
    {
        Id = "MFC_SIH4",
        ModuleId = "PM1",
        DisplayName = "SiH4 MFC",
        GasName = "SiH4",
        Unit = "sccm",
        FullScale = 500,
        Tolerance = 2,
        ToleranceDelay =
            TimeSpan.FromSeconds(3)
    });

//
// 5. 只添加一次
// 先创建零件 → 再创建由这些零件组成的模块
//
hardwareRegistry.Add(sih4Mfc);

//
// 5.1创建 压力剂 定义
//
var pm1PressureGauge =
    new SimulatedPressureGauge(
        new PressureGaugeDefinition
        {
            Id = "PG_PM1",
            ModuleId = "PM1",
            DisplayName = "PM1 Chamber Pressure",
            Unit = "Pa",
            Minimum = 0,
            Maximum = 101_325
        });

hardwareRegistry.Add(pm1PressureGauge);

//
// 5.2创建 干泵 定义
//
var pm1DryPump =
    new SimulatedVacuumPump(
        new VacuumPumpDefinition
        {
            Id = "PUMP_DRY_PM1",
            ModuleId = "PM1",
            DisplayName = "PM1 Dry Pump"
        });

hardwareRegistry.Add(pm1DryPump);

//
// 5.3创建 开关阀 定义
//
var pm1VacuumIsolationValve =
    new SimulatedOnOffValve(
        new OnOffValveDefinition
        {
            Id = "VALVE_PM1_VACUUM_ISOLATION",
            ModuleId = "PM1",
            DisplayName =
                "PM1 Vacuum Isolation Valve",
            SafePosition =
                OnOffValvePosition.Closed,
            MotionTimeout =
                TimeSpan.FromSeconds(5)
        },
        travelTime: TimeSpan.FromSeconds(1));

hardwareRegistry.Add(pm1VacuumIsolationValve);

//
// 5.4 创建PM1加热盘温控器

var pm1HeaterPlant =
    new SimulatedThermalPlant(
        ambientTemperature: 25,
        initialTemperature: 25,
        heatingRatePerSecond: 20,
        passiveRatePerSecond: 5);

var pm1HeaterController =
    new SimulatedTemperatureController(
        new TemperatureControllerDefinition
        {
            Id = "TEMP_CTRL_PM1_HEATER",
            ModuleId = "PM1",
            DisplayName =
                "PM1 Heater Temperature Controller",
            Unit = "°C",
            MinimumSetPoint = 20,
            MaximumSetPoint = 500,
            Tolerance = 3,
            ToleranceDelay =
                TimeSpan.FromSeconds(30)
        },
        pm1HeaterPlant,
        controlDeadband: 1);

hardwareRegistry.Add(pm1HeaterController);

//
// 5.5 创建PM1加热盘监控温度传感器
//
var pm1HeaterMonitorSensor =
    new SimulatedTemperatureSensor(
        new TemperatureSensorDefinition
        {
            Id = "TEMP_PM1_HEATER_MONITOR",
            ModuleId = "PM1",
            DisplayName =
                "PM1 Heater Monitor Temperature",
            Unit = "°C",
            Minimum = -50,
            Maximum = 600
        },
        pm1HeaterPlant);

hardwareRegistry.Add(pm1HeaterMonitorSensor);

//
// 5.6 创建PM1 RF电源
//
var pm1RfPowerSupply =
    new SimulatedRfPowerSupply(
        new RfPowerSupplyDefinition
        {
            Id = "RF_PM1",
            ModuleId = "PM1",
            DisplayName =
                "PM1 RF Power Supply",
            Unit = "W",

            // 13.56 MHz
            FrequencyHz = 13_560_000,

            MaximumPower = 1000,

            ReflectedPowerLimit = 100,

            Tolerance = 10,

            ReflectedPowerDelay =
                TimeSpan.FromSeconds(1),

            ToleranceDelay =
                TimeSpan.FromSeconds(3)
        },
        responseFactor: 0.2,
        reflectedPowerRatio: 0.02);

hardwareRegistry.Add(pm1RfPowerSupply);

//
// 5.7 创建PM1 RF匹配器
//
var pm1RfMatcher =
    new SimulatedRfMatcher(
        new RfMatcherDefinition
        {
            Id = "MATCH_PM1",
            ModuleId = "PM1",
            DisplayName =
                "PM1 RF Matcher",
            PositionUnit = "%",
            MinimumPosition = 0,
            MaximumPosition = 100,
            PositionTolerance = 1,
            PositionToleranceDelay =
                TimeSpan.FromSeconds(3)
        },
        responseFactor: 0.2,
        automaticTuneTarget: 60,
        automaticLoadTarget: 40);

hardwareRegistry.Add(pm1RfMatcher);

//
// 6.创建 PM1 定义
// 这里保存的是稳定硬件 ID，不是 sih4Mfc 对象本身。
var pm1Definition = new ChamberDefinition
{
    Id = "PM1",
    DisplayName = "Process Module 1",

    GasLines =
    [
        new ChamberGasLineDefinition
        {
            GasName = "SiH4",
            MfcId = "MFC_SIH4"
        }
    ],

    Vacuum =
    new ChamberVacuumDefinition
    {
        PressureGaugeId = "PG_PM1",
        DryPumpId = "PUMP_DRY_PM1"
    }
};

//
// 7.创建并注册 PM1 Controller
//
var pm1Interlocks =
    new SimulatedChamberInterlockSource();

var pm1Controller = new ChamberController(
    pm1Definition,
    hardwareRegistry,
    pm1Interlocks);

moduleRegistry.Add(pm1Controller);

//
// 8. 基础运行状态
//
app.MapGet(
    "/",
    () => "Kwy PECVD RT is running.");

//
// 9. 从Registry按IMfc能力查询
//
app.MapGet(
    "/diagnostics/mfcs",
    (IHardwareComponentRegistry
        componentRegistry) =>
    {
        return componentRegistry
            .GetAll<IMfc>()
            .Select(mfc => mfc.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/pressure-gauges",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<IPressureGauge>()
            .Select(
                gauge => gauge.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/vacuum-pumps",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<IVacuumPump>()
            .Select(pump => pump.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/valves",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<IOnOffValve>()
            .Select(valve => valve.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/chambers",
    (IEquipmentModuleRegistry registry) =>
    {
        return registry
            .GetAll<IChamberController>()
            .Select(chamber => chamber.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/temperature-sensors",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<ITemperatureSensor>()
            .Select(sensor => sensor.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/temperature-controllers",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<ITemperatureController>()
            .Select(controller => controller.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/rf-power-supplies",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<IRfPowerSupply>()
            .Select(
                powerSupply =>
                    powerSupply.Snapshot)
            .ToArray();
    });

app.MapGet(
    "/diagnostics/rf-matchers",
    (IHardwareComponentRegistry registry) =>
    {
        return registry
            .GetAll<IRfMatcher>()
            .Select(
                matcher =>
                    matcher.Snapshot)
            .ToArray();
    });


//
// test - 命令路由
//
app.MapPost(
    "/commands/chambers/gas-flow",
    async (
        SetGasFlowRequest request,
        IEquipmentModuleRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ChamberId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Chamber id is required."));
        }

        if (string.IsNullOrWhiteSpace(
                request.GasName))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Gas name is required."));
        }

        if (!double.IsFinite(
                request.RampSeconds) ||
            request.RampSeconds < 0 ||
            request.RampSeconds >
                TimeSpan.MaxValue.TotalSeconds)
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Ramp seconds must be a finite " +
                    "non-negative number."));
        }

        if (!registry.TryGet<
                IChamberController>(
                request.ChamberId,
                out var chamber))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Chamber '{request.ChamberId}' " +
                    "was not found."));
        }

        var result =
            await chamber.SetGasFlowAsync(
                request.GasName,
                request.Target,
                TimeSpan.FromSeconds(
                    request.RampSeconds),
                cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/chambers/initialize",
    async (
        InitializeChamberRequest request,
        IEquipmentModuleRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ChamberId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Chamber id is required."));
        }

        if (!registry.TryGet<IChamberController>(
                request.ChamberId,
                out var chamber))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"Chamber '{request.ChamberId}' " +
                    "was not found."));
        }

        var result = await chamber.InitializeAsync(
            cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/chambers/gas-preparation",
    async (
        StartGasPreparationRequest request,
        IEquipmentModuleRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ChamberId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Chamber id is required."));
        }

        if (request.Gases.Count == 0)
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "At least one gas target is required."));
        }

        foreach (var gas in request.Gases)
        {
            if (string.IsNullOrWhiteSpace(
                    gas.GasName))
            {
                return Results.BadRequest(
                    CommandResult.Failure(
                        ResultCodes.InvalidArgument,
                        "Gas name is required."));
            }

            if (!double.IsFinite(
                    gas.RampSeconds) ||
                gas.RampSeconds < 0 ||
                gas.RampSeconds >
                    TimeSpan.MaxValue.TotalSeconds)
            {
                return Results.BadRequest(
                    CommandResult.Failure(
                        ResultCodes.InvalidArgument,
                        "Ramp seconds must be a finite " +
                        "non-negative number."));
            }
        }

        if (!registry.TryGet<IChamberController>(
                request.ChamberId,
                out var chamber))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"Chamber '{request.ChamberId}' " +
                    "was not found."));
        }

        var targets = request.Gases
            .Select(
                gas => new ChamberGasTarget
                {
                    GasName = gas.GasName,
                    Target = gas.Target,
                    RampDuration =
                        TimeSpan.FromSeconds(
                            gas.RampSeconds)
                })
            .ToArray();

        var result =
            await chamber.StartGasPreparationAsync(
                targets,
                cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/valves/position",
    async (
        SetValvePositionRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ValveId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Valve id is required."));
        }

        if (!Enum.IsDefined(request.Position))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Valve position " +
                    $"'{request.Position}' is invalid."));
        }

        if (!registry.TryGet<IOnOffValve>(
                request.ValveId,
                out var valve))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"Valve '{request.ValveId}' " +
                    "was not found."));
        }

        var result =
            request.Position switch
            {
                OnOffValvePosition.Open =>
                    await valve.OpenAsync(
                        cancellationToken),

                OnOffValvePosition.Closed =>
                    await valve.CloseAsync(
                        cancellationToken),

                _ => CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    $"Valve position " +
                    $"'{request.Position}' " +
                    "is not supported.")
            };

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/temperature-controllers/target",
    async (
        SetTemperatureTargetRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ControllerId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Temperature controller id " +
                    "is required."));
        }

        if (!double.IsFinite(request.Target))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Temperature target must " +
                    "be finite."));
        }

        if (!registry.TryGet<
                ITemperatureController>(
                request.ControllerId,
                out var controller))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"Temperature controller " +
                    $"'{request.ControllerId}' " +
                    "was not found."));
        }

        var result =
            await controller
                .SetTargetTemperatureAsync(
                    request.Target,
                    cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/temperature-controllers/enabled",
    async (
        SetTemperatureControllerEnabledRequest
            request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.ControllerId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "Temperature controller id " +
                    "is required."));
        }

        if (!registry.TryGet<
                ITemperatureController>(
                request.ControllerId,
                out var controller))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"Temperature controller " +
                    $"'{request.ControllerId}' " +
                    "was not found."));
        }

        var result =
            request.Enabled
                ? await controller.EnableAsync(
                    cancellationToken)
                : await controller.DisableAsync(
                    cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/rf-power-supplies/power",
    async (
        SetRfPowerRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.PowerSupplyId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF power supply id " +
                    "is required."));
        }

        if (!double.IsFinite(
                request.Power))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF power must be finite."));
        }

        if (!registry.TryGet<
                IRfPowerSupply>(
                request.PowerSupplyId,
                out var powerSupply))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"RF power supply " +
                    $"'{request.PowerSupplyId}' " +
                    "was not found."));
        }

        var result =
            await powerSupply.SetPowerAsync(
                request.Power,
                cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/rf-power-supplies/enabled",
    async (
        SetRfPowerSupplyEnabledRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.PowerSupplyId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF power supply id " +
                    "is required."));
        }

        if (!registry.TryGet<
                IRfPowerSupply>(
                request.PowerSupplyId,
                out var powerSupply))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"RF power supply " +
                    $"'{request.PowerSupplyId}' " +
                    "was not found."));
        }

        var result =
            request.Enabled
                ? await powerSupply.EnableAsync(
                    cancellationToken)
                : await powerSupply.DisableAsync(
                    cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/rf-matchers/mode",
    async (
        SetRfMatcherModeRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.MatcherId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF matcher id is required."));
        }

        if (!registry.TryGet<IRfMatcher>(
                request.MatcherId,
                out var matcher))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"RF matcher " +
                    $"'{request.MatcherId}' " +
                    "was not found."));
        }

        var result =
            await matcher.SetModeAsync(
                request.Mode,
                cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

app.MapPost(
    "/commands/rf-matchers/manual-position",
    async (
        SetRfMatcherManualPositionRequest request,
        IHardwareComponentRegistry registry,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(
                request.MatcherId))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF matcher id is required."));
        }

        if (!double.IsFinite(
                request.TunePosition) ||
            !double.IsFinite(
                request.LoadPosition))
        {
            return Results.BadRequest(
                CommandResult.Failure(
                    ResultCodes.InvalidArgument,
                    "RF matcher positions must " +
                    "be finite."));
        }

        if (!registry.TryGet<IRfMatcher>(
                request.MatcherId,
                out var matcher))
        {
            return Results.NotFound(
                CommandResult.Failure(
                    ResultCodes.NotReady,
                    $"RF matcher " +
                    $"'{request.MatcherId}' " +
                    "was not found."));
        }

        var result =
            await matcher
                .SetManualPositionAsync(
                    request.TunePosition,
                    request.LoadPosition,
                    cancellationToken);

        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    });

await app.RunAsync();

/*

IoC
├─ IHardwareComponentRegistry
│  └─ MFC_SIH4
│
└─ IEquipmentModuleRegistry
   └─ PM1 ChamberController
      └─ SiH4
         └─ 通过ID引用MFC_SIH4

RT 后台周期只遍历硬件 Registry：MFC_SIH4.ExecuteCycleAsync()

 */
