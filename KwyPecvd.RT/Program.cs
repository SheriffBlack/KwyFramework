using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Simulated;
using KwyPecvd.Process.Chambers;
using KwyPecvd.Process.Equipment;
using KwyPecvd.RT.Requests;
using KwyPecvd.RT.Runtime;
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
    ]
};

//
// 7.创建并注册 PM1 Controller
//
var pm1Controller = new ChamberController(
    pm1Definition,
    hardwareRegistry);

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
    "/diagnostics/chambers",
    (IEquipmentModuleRegistry registry) =>
    {
        return registry
            .GetAll<IChamberController>()
            .Select(chamber => chamber.Snapshot)
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