using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Core;
using Kwy.Device.MotionCard.Core.Safety;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.MotionCard.Googol;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册一张固高运动控制卡及其独立运行时。
    /// 调用前必须通过 <c>AddAxisDefinitions</c> 注册该卡 <c>DeviceId</c> 对应的业务轴定义，
    /// 以便适配器建立业务轴与物理轴通道的映射。
    /// </summary>
    public static IServiceCollection AddGoogolMotionCard(
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

        services.AddMotionCardCore();
        Lazy<GoogolMotionCardDevice>? device = null;
        GoogolMotionCardDevice GetDevice(IServiceProvider provider) =>
            LazyInitializer.EnsureInitialized(ref device, () => new Lazy<GoogolMotionCardDevice>(() => new GoogolMotionCardDevice(
                config,
                provider.GetRequiredService<IAxisDefinitionProvider>()))).Value;

        services.AddSingleton<GoogolMotionCardDevice>(GetDevice);
        services.AddSingleton<IDevice>(GetDevice);

        var stateMonitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(stateMonitorOptions);

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            GoogolMotionCardDevice card = GetDevice(provider);
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
