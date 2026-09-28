using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kwy.Device.MotionCard.Abstractions.Operations;
using Kwy.Device.MotionCard.Abstractions.Groups;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Controller;
using Kwy.Device.MotionCard.Abstractions.Kinematics.Planning;
using Kwy.Device.MotionCard.Abstractions.Synchronization;
using Kwy.Device.MotionCard.Abstractions.Kinematics;
using Kwy.Device.MotionCard.Abstractions.Kinematics.OfflinePlanning.Jacobian;
using Kwy.Device.MotionCard.Abstractions.Configuration;
using Kwy.Device.MotionCard.Abstractions.Kinematics.OfflinePlanning.Timing;
using Kwy.Device.MotionCard.Core.Synchronization;
using Kwy.Device.MotionCard.Core.Groups;
using Kwy.Device.MotionCard.Core.Operations;
using Kwy.Device.MotionCard.Core.Kinematics.Controller;
using Kwy.Device.MotionCard.Core.Kinematics;
using Kwy.Device.MotionCard.Core.Kinematics.OfflinePlanning.Timing;
using Kwy.Device.MotionCard.Core.Kinematics.Planning;
using Kwy.Device.MotionCard.Core.Configuration;
using Kwy.Device.MotionCard.Core.Axes;
using Kwy.Device.MotionCard.Core.Safety;
using Kwy.Device.MotionCard.Core.Kinematics.OfflinePlanning.Jacobian;

namespace Kwy.Device.MotionCard.Core;

/// <summary>运动领域服务注册入口。</summary>
public static class ServiceCollectionExtensions
{    public static IServiceCollection AddMotionStateMonitor(
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

    public static IServiceCollection AddAxisDefinitions(
        this IServiceCollection services,
        IEnumerable<AxisDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        AxisDefinition[] items = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        services.AddSingleton<IAxisDefinitionProvider>(_ => new AxisDefinitionProvider(items));
        return services;
    }

    public static IServiceCollection AddMotionCardCore(
        this IServiceCollection services,
        Action<MotionAdmissionOptions>? configureAdmission = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();

        var admissionOptions = new MotionAdmissionOptions();
        configureAdmission?.Invoke(admissionOptions);

        services.TryAddSingleton(admissionOptions);
        services.TryAddSingleton<IMotionRuntimeRegistry, MotionRuntimeRegistry>();
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

    public static IServiceCollection AddMotionGroupDefinitions(
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
            provider.GetRequiredService<IAxisDefinitionProvider>(),
            provider.GetRequiredService<IMotionGroupDefinitionProvider>(),
            pointDefinitions,
            provider.GetService<IVirtualAxisDefinitionProvider>(),
            provider.GetService<IMotionSynchronizationDefinitionProvider>()));
        services.AddSingleton<IMotionAutoModeGate, MotionAutoModeGate>();
        services.AddSingleton<IMotionGroupExecutor, MotionGroupExecutor>();
        return services;
    }

    public static IServiceCollection AddMotionSynchronizationDefinitions(
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

    public static IServiceCollection AddSpatialMotion(
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

    public static IServiceCollection AddOfflineMotionPlanning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IJointTrajectoryTimeParameterizer, JointTrajectoryTimeParameterizer>();
        services.TryAddSingleton<ICartesianVelocityLimiter, CartesianVelocityLimiter>();
        return services;
    }
}
