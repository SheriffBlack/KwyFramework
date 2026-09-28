using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Simulation;

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

        services.AddSingleton<SimulationMotionCardDevice>(provider => new SimulationMotionCardDevice(
            config,
            provider.GetRequiredService<IAxisDefinitionProvider>()));

        var monitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(monitorOptions);
        services.AddSingleton(monitorOptions);
        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            SimulationMotionCardDevice card = provider.GetRequiredService<SimulationMotionCardDevice>();
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
