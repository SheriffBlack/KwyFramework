using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Core;
using Kwy.Device.Motion.Core.Safety;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Motion.Simulation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSimulationMotionCard(
        this IServiceCollection services,
        Action<SimulationMotionCardConfig>? configure = null,
        Action<MotionStateMonitorOptions>? configureStateMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var config = new SimulationMotionCardConfig();
        configure?.Invoke(config);
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid simulation motion card configuration.", nameof(configure));
        }

        services.AddMotionCore();
        Lazy<SimulationMotionCardDevice>? device = null;
        SimulationMotionCardDevice GetDevice(IServiceProvider provider) =>
            LazyInitializer.EnsureInitialized(ref device, () => new Lazy<SimulationMotionCardDevice>(() => new SimulationMotionCardDevice(
                config,
                provider.GetRequiredService<IAxisDefinitionProvider>()))).Value;

        services.AddSingleton<SimulationMotionCardDevice>(GetDevice);
        services.AddSingleton<IDevice>(GetDevice);

        var monitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(monitorOptions);
        services.AddSingleton(monitorOptions);
        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            SimulationMotionCardDevice card = GetDevice(provider);
            monitorOptions.Axes = card.Axes.Select(static axis => axis.Channel).ToArray();
            monitorOptions.Validate();
            return MotionRuntimeFactory.Create(
                card,
                monitorOptions,
                provider.GetService<MotionAdmissionOptions>(),
                provider.GetService<IAxisHomeLifecycle>());
        });

        return services;
    }
}
