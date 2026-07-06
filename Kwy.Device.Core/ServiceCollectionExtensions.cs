using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Abstractions.Sessions;
using Kwy.Device.Abstractions.Vision;
using Kwy.Device.Core.Equipment;
using Kwy.Device.Core.IO;
using Kwy.Device.Core.Motion;
using Kwy.Device.Core.Sessions;
using Kwy.Device.Core.Vision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyDeviceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDeviceFactory, DeviceFactory>();
        services.TryAddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.TryAddSingleton<IIoStateMonitor, IoStateMonitor>();
        services.TryAddSingleton<IHardwareInterruptWaiter>(provider => provider.GetRequiredService<IIoStateMonitor>());
        services.TryAddSingleton<ICameraRegistry, CameraRegistry>();
        services.TryAddSingleton<DeviceSafetyOptions>();
        services.TryAddSingleton<IDeviceStateSynchronizer, CompositeDeviceStateSynchronizer>();
        services.TryAddSingleton<IDeviceSafetyGuard, CompositeDeviceSafetyGuard>();
        services.TryAddSingleton<IDeviceRecoveryService, DeviceRecoveryService>();
        services.TryAddSingleton<IEquipmentStateMachine, EquipmentStateMachine>();
        services.TryAddSingleton<IEquipmentModeService, EquipmentModeService>();
        services.TryAddSingleton<IEquipmentEventSink, InMemoryEquipmentEventSink>();
        services.TryAddSingleton<IAlarmService, InMemoryAlarmService>();
        services.TryAddSingleton<IAuditTrail, InMemoryAuditTrail>();
        services.TryAddSingleton<IRecipeRepository, InMemoryRecipeRepository>();
        services.TryAddSingleton<IRecipeValidator, DefaultRecipeValidator>();
        services.TryAddSingleton<IRecipeApplier, NoOpRecipeApplier>();
        services.TryAddSingleton<IRecipeService, RecipeService>();
        services.TryAddSingleton<IEquipmentRecoveryOrchestrator, EquipmentRecoveryOrchestrator>();
        services.TryAddSingleton<IEquipmentProcessController, EquipmentProcessController>();
        services.TryAddSingleton<ITransactionManager, InMemoryTransactionManager>();

        return services;
    }

    public static IServiceCollection AddKwyDeviceRecoveryFor<TDevice>(
        this IServiceCollection services)
        where TDevice : class, IDevice
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IDeviceStateSynchronizer>(provider =>
            new DefaultDeviceStateSynchronizer(provider.GetRequiredService<TDevice>())));

        return services;
    }

    public static IServiceCollection AddKwyMotionStateMonitor(
        this IServiceCollection services,
        Action<MotionStateMonitorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MotionStateMonitorOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton<IMotionStateMonitor, MotionStateMonitor>();
        services.TryAddSingleton<IMotionStateProvider>(provider => provider.GetRequiredService<IMotionStateMonitor>());

        return services;
    }

    public static IServiceCollection AddKwyMotionServices(
        this IServiceCollection services,
        Action<MotionSafetyOptions>? configureSafety = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var safetyOptions = new MotionSafetyOptions();
        configureSafety?.Invoke(safetyOptions);

        services.TryAddSingleton(safetyOptions);
        services.TryAddSingleton<IMotionRuntimeRegistry, MotionRuntimeRegistry>();
        services.TryAddSingleton<IMotionStateMonitor>(provider =>
            provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().StateMonitor);
        services.TryAddSingleton<IMotionStateProvider>(provider => provider.GetRequiredService<IMotionStateMonitor>());
        services.TryAddSingleton<IMotionSafetyGuard, MotionSafetyGuard>();
        services.TryAddSingleton<SafeAxisMotionController>();
        services.TryAddSingleton<ISafeAxisMotionController>(provider => provider.GetRequiredService<SafeAxisMotionController>());
        services.TryAddSingleton<IAxisMotionExecutor>(provider =>
            provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().AxisExecutor);
        services.TryAddSingleton<INamedPositionRepository, InMemoryNamedPositionRepository>();
        services.TryAddSingleton<INamedPositionMotionService, NamedPositionMotionService>();

        return services;
    }
}
