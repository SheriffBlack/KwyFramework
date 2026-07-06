using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Simulation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwySimulationMotionCard(
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

        var device = new Lazy<SimulationMotionCardDevice>(() => new SimulationMotionCardDevice(config));
        services.AddSingleton(_ => device.Value);

        var monitorOptions = new MotionStateMonitorOptions { FirstAxis = 1, AxisCount = config.AxisCount };
        configureStateMonitor?.Invoke(monitorOptions);
        monitorOptions.Validate();
        services.AddSingleton(monitorOptions);
        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            SimulationMotionCardDevice card = device.Value;
            var monitor = new MotionStateMonitor(card, monitorOptions);
            var safety = new MotionSafetyGuard(
                card,
                monitor,
                provider.GetService<MotionSafetyOptions>() ?? new MotionSafetyOptions());
            var executor = new AxisMotionExecutor(card, card, monitor, safety);
            return new MotionDeviceRuntime(card, monitor, executor);
        });

        return services;
    }
}
