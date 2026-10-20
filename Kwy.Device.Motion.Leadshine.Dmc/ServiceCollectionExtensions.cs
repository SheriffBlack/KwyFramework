using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Core;
using Kwy.Device.Motion.Core.Safety;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Motion.Leadshine.Dmc;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册一张雷赛 DMC3800 系列脉冲型运动控制卡及其独立运行时。
    /// 调用前必须通过 <c>AddAxisDefinitions</c> 注册此卡 <c>DeviceId</c> 对应的业务轴定义，
    /// 适配器据此建立业务轴与雷赛物理通道之间的映射。
    /// </summary>
    public static IServiceCollection AddLeadshineDmc3800(
        this IServiceCollection services,
        Action<Dmc3800Config>? configure = null,
        Action<MotionStateMonitorOptions>? configureStateMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new Dmc3800Config();
        configure?.Invoke(config);

        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Leadshine DMC3800 configuration.", nameof(configure));
        }

        services.AddMotionCore();
        Lazy<Dmc3800Device>? device = null;
        Dmc3800Device GetDevice(IServiceProvider provider) =>
            LazyInitializer.EnsureInitialized(ref device, () => new Lazy<Dmc3800Device>(() => new Dmc3800Device(
                config,
                provider.GetRequiredService<IAxisDefinitionProvider>()))).Value;

        services.AddSingleton<Dmc3800Device>(GetDevice);
        services.AddSingleton<IDevice>(GetDevice);

        var stateMonitorOptions = new MotionStateMonitorOptions();
        configureStateMonitor?.Invoke(stateMonitorOptions);

        services.AddSingleton<IMotionDeviceRuntime>(provider =>
        {
            Dmc3800Device card = GetDevice(provider);
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
