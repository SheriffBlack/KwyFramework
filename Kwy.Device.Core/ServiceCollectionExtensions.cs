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
        services.TryAddSingleton<ILogicalIoService>(provider => provider.GetRequiredService<IIoStateMonitor>());
        services.TryAddSingleton<ILogicalIoReader>(provider => provider.GetRequiredService<IIoStateMonitor>());
        services.TryAddSingleton<ILogicalIoWriter>(provider => provider.GetRequiredService<IIoStateMonitor>());
        services.TryAddSingleton<IIoSafetyController>(provider => provider.GetRequiredService<IIoStateMonitor>());
        services.TryAddSingleton<IIoStateSubscription>(provider => provider.GetRequiredService<IIoStateMonitor>());
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
        services.TryAddSingleton<IMotionSafetyGuard>(provider =>
        {
            IMotionDeviceRuntime runtime = provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle();
            return new MotionSafetyGuard(runtime.Card, runtime.StateMonitor, safetyOptions, provider.GetRequiredService<IAxisHomeLifecycle>());
        });
        services.TryAddSingleton<SafeAxisMotionController>(provider =>
        {
            IMotionCard card = provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().Card;
            if (card is not IAxisMotionController controller
                || card is not IMotionProfileController profileController
                || card is not IAxisStatusReader statusReader)
            {
                throw new InvalidOperationException($"Motion card '{card.DeviceId}' does not provide standard single-axis motion capabilities.");
            }

            return new SafeAxisMotionController(
                controller,
                profileController,
                statusReader,
                provider.GetRequiredService<IMotionSafetyGuard>(),
                card as IAxisDefinitionProvider,
                provider.GetRequiredService<IAxisHomeLifecycle>());
        });
        services.TryAddSingleton<ISafeAxisMotionController>(provider => provider.GetRequiredService<SafeAxisMotionController>());
        services.TryAddSingleton<IAxisMotionExecutor>(provider =>
            provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().AxisExecutor);
        services.TryAddSingleton<IBusinessAxisMotionExecutor, BusinessAxisMotionExecutor>();
        services.TryAddSingleton<IMotionResourceLock, MotionResourceLock>();
        services.TryAddSingleton<MotionOperationTracker>();
        services.TryAddSingleton<IMotionOperationTracker>(provider => provider.GetRequiredService<MotionOperationTracker>());
        services.TryAddSingleton<IAxisHomeLifecycle, AxisHomeLifecycle>();
        services.TryAddSingleton<INamedPositionRepository, InMemoryNamedPositionRepository>();
        services.TryAddSingleton<INamedPositionMotionService, NamedPositionMotionService>();
        services.TryAddSingleton<IAxisCoordinateTransformer>(provider =>
            new AxisCoordinateTransformer(provider.GetService<IAxisErrorCompensationProvider>()));
        services.TryAddSingleton<IRotaryAxisPathPlanner, RotaryAxisPathPlanner>();

        return services;
    }

    /// <summary>注册启动期运动组配置、配置校验与自动模式门禁。</summary>
    public static IServiceCollection AddKwyMotionGroups(
        this IServiceCollection services,
        IEnumerable<MotionGroupDefinition> groups,
        IEnumerable<IoPoint>? ioPoints = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var groupDefinitions = groups?.ToArray() ?? throw new ArgumentNullException(nameof(groups));
        var pointDefinitions = ioPoints?.ToArray() ?? Array.Empty<IoPoint>();
        services.AddSingleton<IMotionGroupDefinitionProvider>(_ => new MotionGroupDefinitionProvider(groupDefinitions));
        services.AddSingleton<IMotionConfigurationValidator>(provider => new MotionConfigurationValidator(
            provider.GetRequiredService<IMotionRuntimeRegistry>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            pointDefinitions,
            provider.GetService<IVirtualAxisDefinitionProvider>(),
            provider.GetService<IMotionSynchronizationDefinitionProvider>()));
        services.AddSingleton<IMotionAutoModeGate, MotionAutoModeGate>();
        services.AddSingleton<IMotionGroupExecutor, MotionGroupExecutor>();
        return services;
    }

    /// <summary>注册虚拟轴、电子齿轮与电子凸轮的设备配置定义。</summary>
    public static IServiceCollection AddKwyMotionSynchronizations(
        this IServiceCollection services,
        IEnumerable<VirtualAxisDefinition>? virtualAxes = null,
        IEnumerable<ElectronicGearDefinition>? electronicGears = null,
        IEnumerable<ElectronicCamDefinition>? electronicCams = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        VirtualAxisDefinition[] virtualAxisDefinitions = virtualAxes?.ToArray() ?? [];
        ElectronicGearDefinition[] gearDefinitions = electronicGears?.ToArray() ?? [];
        ElectronicCamDefinition[] camDefinitions = electronicCams?.ToArray() ?? [];
        services.AddSingleton<MotionSynchronizationDefinitionProvider>(_ => new MotionSynchronizationDefinitionProvider(
            virtualAxisDefinitions,
            gearDefinitions,
            camDefinitions));
        services.AddSingleton<IVirtualAxisDefinitionProvider>(provider => provider.GetRequiredService<MotionSynchronizationDefinitionProvider>());
        services.AddSingleton<IMotionSynchronizationDefinitionProvider>(provider => provider.GetRequiredService<MotionSynchronizationDefinitionProvider>());
        return services;
    }

    /// <summary>
    /// 注册空间坐标与机构运动入口。
    /// 调用方需额外注册对应机构的 IKinematicsSolver；框架不假设任何六轴机构的几何尺寸或逆解公式。
    /// </summary>
    public static IServiceCollection AddKwySpatialMotion(
        this IServiceCollection services,
        IEnumerable<CoordinateFrameDefinition> coordinateFrames,
        IEnumerable<KinematicMechanismDefinition> mechanisms)
    {
        ArgumentNullException.ThrowIfNull(services);
        CoordinateFrameDefinition[] frameDefinitions = coordinateFrames?.ToArray() ?? throw new ArgumentNullException(nameof(coordinateFrames));
        KinematicMechanismDefinition[] mechanismDefinitions = mechanisms?.ToArray() ?? throw new ArgumentNullException(nameof(mechanisms));
        services.AddSingleton<CoordinateTransformService>(_ => new CoordinateTransformService(frameDefinitions));
        services.AddSingleton<ICoordinateTransformService>(provider => provider.GetRequiredService<CoordinateTransformService>());
        services.AddSingleton<ICoordinateFrameRegistry>(provider => provider.GetRequiredService<CoordinateTransformService>());
        services.TryAddSingleton<ICartesianTrajectoryPlanner, CartesianTrajectoryPlanner>();
        services.AddSingleton<IMotionPlanningPipeline>(provider => new MotionPlanningPipeline(
            mechanismDefinitions,
            provider.GetServices<IKinematicsSolver>(),
            provider.GetRequiredService<ICoordinateFrameRegistry>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            provider.GetRequiredService<IMotionRuntimeRegistry>(),
            provider.GetRequiredService<ICartesianTrajectoryPlanner>()));
        services.AddSingleton<IPoseMotionExecutor>(provider => new PoseMotionExecutor(
            mechanismDefinitions,
            provider.GetServices<IKinematicsSolver>(),
            provider.GetRequiredService<ICoordinateTransformService>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            provider.GetRequiredService<IMotionGroupExecutor>(),
            provider.GetRequiredService<IMotionRuntimeRegistry>()));
        return services;
    }
}
