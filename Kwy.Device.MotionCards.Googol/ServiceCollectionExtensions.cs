using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Googol;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyGoogolMotionCard(
        this IServiceCollection services,
        Action<GoogolMotionCardConfig>? configure = null,
        Action<MotionStateMonitorOptions>? configureStateMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new GoogolMotionCardConfig();
        configure?.Invoke(config);

        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Googol motion card configuration.", nameof(configure));
        }

        services.AddSingleton<GoogolMotionCardDevice>(provider => new GoogolMotionCardDevice(
            config,
            provider.GetRequiredService<IAxisDefinitionProvider>()));

        var stateMonitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(stateMonitorOptions);

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            GoogolMotionCardDevice card = provider.GetRequiredService<GoogolMotionCardDevice>();
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
