using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions.Vision;
using Kwy.Device.Core.IO;
using Kwy.Device.Core.Motion;
using Kwy.Device.Core.PLC;
using Kwy.Device.Core.Vision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyDeviceCore(
        this IServiceCollection services,
        Action<IoStateMonitorOptions>? configureIoMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var ioMonitorOptions = new IoStateMonitorOptions();
        configureIoMonitor?.Invoke(ioMonitorOptions);
        ioMonitorOptions.Validate();

        services.TryAddSingleton(ioMonitorOptions);
        services.TryAddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.TryAddSingleton<IoStateMonitor>();
        services.TryAddSingleton<IIoStateMonitor>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoReader>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoWriter>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<IProcessOutputStateController>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<IIoStateSubscription>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoInterruptWaiter>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ICameraRegistry, CameraRegistry>();
        return services;
    }

    /// <summary>
    /// 注册设备的统一 IO 点位定义目录。
    /// 调用方在连接完成后将同一目录传给 <see cref="IIoStateMonitor.Initialize"/>，由监视器校验实际设备与通道。
    /// </summary>
    public static IServiceCollection AddKwyIoPointDefinitions(
        this IServiceCollection services,
        IEnumerable<IoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        IoPointDefinition[] items = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        services.AddSingleton<IoPointDefinitionProvider>(_ => new IoPointDefinitionProvider(items));
        services.AddSingleton<IIoPointDefinitionProvider>(provider => provider.GetRequiredService<IoPointDefinitionProvider>());
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

    public static IServiceCollection AddKwyPlcPointDefinitions(
        this IServiceCollection services,
        IEnumerable<PlcPointDefinition> points)
    {
        ArgumentNullException.ThrowIfNull(services);
        PlcPointDefinition[] definitions = points?.ToArray() ?? throw new ArgumentNullException(nameof(points));
        var provider = new PlcPointDefinitionProvider(definitions);

        services.AddSingleton<IPlcPointDefinitionProvider>(provider);
        services.TryAddSingleton<LogicalPlcService>();
        services.TryAddSingleton<ILogicalPlcReader>(provider => provider.GetRequiredService<LogicalPlcService>());
        services.TryAddSingleton<ILogicalPlcWriter>(provider => provider.GetRequiredService<LogicalPlcService>());
        return services;
    }

    public static IServiceCollection AddKwyMotionServices(
        this IServiceCollection services,
        Action<MotionAdmissionOptions>? configureAdmission = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var admissionOptions = new MotionAdmissionOptions();
        configureAdmission?.Invoke(admissionOptions);

        services.TryAddSingleton(admissionOptions);
        services.TryAddSingleton<IMotionRuntimeRegistry, MotionRuntimeRegistry>();
        services.TryAddSingleton<IAxisDefinitionProvider>(provider => new AxisDefinitionProvider(
            provider.GetRequiredService<IMotionRuntimeRegistry>().Runtimes
                .SelectMany(runtime => (runtime.Card as IAxisChannelDefinitionProvider)?.Axes ?? [])));
        services.TryAddSingleton<IMotionStateMonitor>(provider =>
            provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().StateMonitor);
        services.TryAddSingleton<IMotionStateProvider>(provider => provider.GetRequiredService<IMotionStateMonitor>());
        services.TryAddSingleton<IMotionAdmissionGuard>(provider =>
        {
            IMotionDeviceRuntime runtime = provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle();
            return new MotionAdmissionGuard(runtime.Card, runtime.StateMonitor, admissionOptions, provider.GetRequiredService<IAxisHomeLifecycle>());
        });
        services.TryAddSingleton<AdmittedAxisMotionController>(provider =>
        {
            IMotionCard card = provider.GetRequiredService<IMotionRuntimeRegistry>().GetRequiredSingle().Card;
            if (card is not IAxisMotionController controller
                || card is not IMotionProfileController profileController
                || card is not IAxisStatusReader statusReader)
            {
                throw new InvalidOperationException($"Motion card '{card.DeviceId}' does not provide standard single-axis motion capabilities.");
            }

            return new AdmittedAxisMotionController(
                controller,
                profileController,
                statusReader,
                provider.GetRequiredService<IMotionAdmissionGuard>(),
                card as IAxisChannelDefinitionProvider,
                provider.GetRequiredService<IAxisHomeLifecycle>());
        });
        services.TryAddSingleton<IAdmittedAxisMotionController>(provider => provider.GetRequiredService<AdmittedAxisMotionController>());
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
        IEnumerable<IoPointDefinition>? ioPoints = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var groupDefinitions = groups?.ToArray() ?? throw new ArgumentNullException(nameof(groups));
        var pointDefinitions = ioPoints?.ToArray() ?? Array.Empty<IoPointDefinition>();
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
        services.TryAddSingleton<IJointTrajectorySafetyValidator, JointTrajectorySafetyValidator>();
        services.AddSingleton<IControllerMotionProgramService>(provider => new ControllerMotionProgramService(
            mechanismDefinitions,
            provider.GetRequiredService<IMotionPlanningPipeline>(),
            provider.GetRequiredService<ICoordinateFrameRegistry>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            provider.GetRequiredService<IMotionRuntimeRegistry>(),
            provider.GetRequiredService<IMotionResourceLock>(),
            provider.GetRequiredService<IMotionOperationTracker>(),
            provider.GetRequiredService<IAxisHomeLifecycle>(),
            provider.GetRequiredService<IJointTrajectorySafetyValidator>(),
            provider.GetRequiredService<MotionAdmissionOptions>()));
        services.AddSingleton<IPoseMotionExecutor>(provider => new PoseMotionExecutor(
            mechanismDefinitions,
            provider.GetServices<IKinematicsSolver>(),
            provider.GetRequiredService<ICoordinateTransformService>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            provider.GetRequiredService<IMotionGroupExecutor>(),
            provider.GetRequiredService<IMotionRuntimeRegistry>()));
        return services;
    }

    /// <summary>
    /// 注册离线规划辅助能力，用于仿真、配方预检和时间估算。
    /// 这些服务不会驱动控制器周期性下发点位；连续轮廓应由厂商原生程序执行。
    /// </summary>
    public static IServiceCollection AddKwyOfflineMotionPlanning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IJointTrajectoryTimeParameterizer, JointTrajectoryTimeParameterizer>();
        services.TryAddSingleton<ICartesianVelocityLimiter, CartesianVelocityLimiter>();
        return services;
    }
}
