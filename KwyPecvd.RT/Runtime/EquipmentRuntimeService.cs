using KwyPecvd.Device;
using KwyPecvd.Process.Equipment;

namespace KwyPecvd.RT.Runtime;

/*

Module 使用硬件，但硬件周期的调度权统一归 EquipmentRuntimeService。
EquipmentRuntimeService
└─ PeriodicTimer：100ms
   ├─ 1. 遍历 HardwareComponentRegistry
   │    └─ component.ExecuteCycleAsync()
   │         ├─ 写入目标
   │         ├─ 读取真实反馈
   │         └─ 更新 Snapshot
   │
   └─ 2. 遍历 EquipmentModuleRegistry
        └─ module.ExecuteCycleAsync()
             ├─ 读取硬件 Snapshot --> 每个子系统负责读取并解释自己所需的 Component Snapshot
             └─ 推进 PM 状态机

ChamberController
├─ ChamberGasSystem
│  └─ 读取 IMfc.Snapshot
├─ ChamberVacuumSystem
│  ├─ 读取 IPressureGauge.Snapshot
│  ├─ 读取 IVacuumPump.Snapshot
│  └─ 读取 IOnOffValve.Snapshot
├─ ChamberTemperatureSystem
│  ├─ 读取 ITemperatureSensor.Snapshot
│  └─ 读取 ITemperatureController.Snapshot
└─ ChamberRfSystem
   ├─ 读取 IRfPowerSupply.Snapshot
   └─ 读取 IRfMatcher.Snapshot



腔体状态类似气缸运动： 
    下发伸出命令
        → 等待伸出到位传感器
        → 到位后确认动作完成
        → 切换到 Extended
*/

public sealed class EquipmentRuntimeService : BackgroundService
{
    private readonly IHardwareComponentRegistry hardwareRegistry;

    private readonly IEquipmentModuleRegistry moduleRegistry;

    private readonly ILogger<EquipmentRuntimeService> logger;

    public EquipmentRuntimeService(
        IHardwareComponentRegistry hardwareRegistry,
        IEquipmentModuleRegistry moduleRegistry,
        ILogger<EquipmentRuntimeService> logger)
    {
        this.hardwareRegistry = hardwareRegistry;
        this.moduleRegistry = moduleRegistry;
        this.logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var component in hardwareRegistry.Components)
        {
            await component.InitializeAsync(
                cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await timer.WaitForNextTickAsync(
                   stoppingToken))
        {
            await ExecuteHardwareCycleAsync(
                stoppingToken);

            await ExecuteModuleCycleAsync(
                stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var components = hardwareRegistry.Components.ToArray();

        try
        {
            await base.StopAsync(cancellationToken);
        }
        finally
        {
            foreach (var component in components)
            {
                try
                {
                    await component.ShutdownAsync(
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Hardware component {ComponentId} " +
                        "shutdown failed.",
                        component.Id);
                }
            }
        }
    }

    /// <summary>硬件周期 </summary>
    private async Task ExecuteHardwareCycleAsync(CancellationToken cancellationToken)
    {
        var components = hardwareRegistry.Components.ToArray();

        foreach (var component in components)
        {
            try
            {
                await component.ExecuteCycleAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Hardware component {ComponentId} " +
                    "cycle failed.",
                    component.Id);
            }
        }
    }

    /// <summary>模块周期 </summary>
    private async Task ExecuteModuleCycleAsync(CancellationToken cancellationToken)
    {
        var modules = moduleRegistry
            .GetAll<ICyclicEquipmentModuleController>()
            .ToArray();

        foreach (var module in modules)
        {
            try
            {
                await module.ExecuteCycleAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Equipment module {ModuleId} " +
                    "cycle failed.",
                    module.Id);
            }
        }
    }
}