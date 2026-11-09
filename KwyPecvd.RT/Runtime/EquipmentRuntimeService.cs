using KwyPecvd.Device;
using KwyPecvd.Equipment.Commands;
using KwyPecvd.Process.Modules;

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
         ├─ module.ExecuteCycleAsync()
         │    ├─ 读取硬件 Snapshot
         │    └─ 推进 Process 状态机
         │
         └─ commandCompletionObserver.ObserveAsync()
              ├─ 读取模块最终状态
              ├─ 完成 Pending Command
              └─ 发布 Equipment Event

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
    private readonly IEquipmentCommandCompletionObserver commandCompletionObserver;
    private readonly IHardwareComponentRegistry hardwareRegistry;

    private readonly IEquipmentModuleRegistry moduleRegistry;

    private readonly ILogger<EquipmentRuntimeService> logger;

    public EquipmentRuntimeService(
        IHardwareComponentRegistry hardwareRegistry,
        IEquipmentModuleRegistry moduleRegistry,
        IEquipmentCommandCompletionObserver commandCompletionObserver,
        ILogger<EquipmentRuntimeService> logger)
    {
        this.hardwareRegistry = hardwareRegistry ?? throw new ArgumentNullException( nameof(hardwareRegistry));

        this.moduleRegistry = moduleRegistry ?? throw new ArgumentNullException(nameof(moduleRegistry));

        this.commandCompletionObserver = commandCompletionObserver ?? throw new ArgumentNullException(nameof(commandCompletionObserver));

        this.logger = logger ?? throw new ArgumentNullException( nameof(logger));
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
            cancellationToken.ThrowIfCancellationRequested();

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
        var modules = moduleRegistry.GetAll<ICyclicEquipmentModuleController>().ToArray();

        foreach (var module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Process 根据硬件反馈推进模块状态机。
                await module.ExecuteCycleAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Equipment module {ModuleId} cycle failed.",
                    module.Id);

                // 本周期状态推进失败，不能观察旧 Snapshot。
                continue;
            }

            // 模块周期完成期间，也可能刚刚发生取消。
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Equipment 根据推进后的最终状态完成异步命令。
                await commandCompletionObserver.ObserveAsync(
                    module,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Equipment command completion observation " +
                    "failed for module {ModuleId}.",
                    module.Id);
            }
        }
    }
}