using Kwy.Device.Io.Abstractions;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.MotionCards.Leadshine;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册一张雷赛运动控制卡及其独立运行时。
    /// 调用前必须通过 <c>AddAxisDefinitions</c> 注册此卡 <c>DeviceId</c> 对应的业务轴定义，
    /// 适配器据此建立业务轴与雷赛物理通道之间的映射。
    /// </summary>
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
