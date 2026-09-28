using Kwy.Device.Io.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Motion;
using Kwy.Device.MotionCard.Abstractions.Motion.Axes;
using Kwy.Device.MotionCard.Core.Motion;
using Kwy.Device.MotionCard.Core.Motion.Safety;
using Microsoft.Extensions.DependencyInjection;

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
