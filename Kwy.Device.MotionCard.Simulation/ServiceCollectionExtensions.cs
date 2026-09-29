using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Core;
using Kwy.Device.MotionCard.Core.Safety;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.MotionCard.Simulation;

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

        services.AddMotionCardCore();
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
