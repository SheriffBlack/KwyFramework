using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions;
using Kwy.Device.MotionCard.Abstractions.Axes;
using Kwy.Device.MotionCard.Core;
using Kwy.Device.MotionCard.Core.Safety;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.MotionCard.Leadshine;

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

        services.AddMotionCardCore();
        Lazy<LeadshineMotionCardDevice>? device = null;
        LeadshineMotionCardDevice GetDevice(IServiceProvider provider) =>
            LazyInitializer.EnsureInitialized(ref device, () => new Lazy<LeadshineMotionCardDevice>(() => new LeadshineMotionCardDevice(
                config,
                provider.GetRequiredService<IAxisDefinitionProvider>()))).Value;

        services.AddSingleton<LeadshineMotionCardDevice>(GetDevice);
        services.AddSingleton<IDevice>(GetDevice);

        var stateMonitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(stateMonitorOptions);

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            LeadshineMotionCardDevice card = GetDevice(provider);
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
