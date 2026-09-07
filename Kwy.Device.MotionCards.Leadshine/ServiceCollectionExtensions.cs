using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Leadshine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyLeadshineMotionCard(
        this IServiceCollection services,
        Action<LeadshineMotionCardConfig>? configure = null,
        Action<MotionStateMonitorOptions>? configureStateMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new LeadshineMotionCardConfig();
        configure?.Invoke(config);

        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Leadshine motion card configuration.", nameof(configure));
        }

        var device = new Lazy<LeadshineMotionCardDevice>(() => new LeadshineMotionCardDevice(config));
        services.AddSingleton(_ => device.Value);

        var stateMonitorOptions = new MotionStateMonitorOptions
        {
            FirstAxis = 1,
            AxisCount = config.AxisCount
        };
        configureStateMonitor?.Invoke(stateMonitorOptions);
        stateMonitorOptions.Validate();

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            LeadshineMotionCardDevice card = device.Value;
            var monitor = new MotionStateMonitor(card, stateMonitorOptions);
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
