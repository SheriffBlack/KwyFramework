using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Leadshine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLeadshineMotionCard(
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

        services.AddSingleton<LeadshineMotionCardDevice>(provider => new LeadshineMotionCardDevice(
            config,
            provider.GetRequiredService<IAxisDefinitionProvider>()));

        var stateMonitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(stateMonitorOptions);

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            LeadshineMotionCardDevice card = provider.GetRequiredService<LeadshineMotionCardDevice>();
            stateMonitorOptions.Axes = card.Axes.Select(static axis => axis.Channel).ToArray();
            stateMonitorOptions.Validate();
            return MotionRuntimeFactory.Create(
                card,
                stateMonitorOptions,
                provider.GetService<MotionAdmissionOptions>(),
                provider.GetService<IAxisHomeLifecycle>());
        });

        return services;
    }
}
