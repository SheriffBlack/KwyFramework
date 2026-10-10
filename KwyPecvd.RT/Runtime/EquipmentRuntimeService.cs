using KwyPecvd.Device;
using KwyPecvd.Process.Equipment;

namespace KwyPecvd.RT.Runtime;

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