using Kwy.Device.Abstractions;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.PLC.Beckhoff;

/// <summary>TwinCAT ADS PLC 适配器的依赖注入注册入口。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册一台通过 TwinCAT ADS 访问的倍福 PLC。</summary>
    public static IServiceCollection AddBeckhoffPLC(
        this IServiceCollection services,
        string deviceId,
        string deviceName,
        Action<BeckhoffAdsPlcConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddPLCCore();
        var config = new BeckhoffAdsPlcConfig();
        configure(config);
        if (!config.Validate())
        {
            throw new ArgumentException("TwinCAT ADS PLC 配置无效。", nameof(configure));
        }

        var device = new Lazy<BeckhoffAdsPlcDevice>(() => new BeckhoffAdsPlcDevice(deviceId, deviceName, config));
        services.AddSingleton(_ => device.Value);
        services.AddSingleton<IPlcDevice>(_ => device.Value);
        services.AddSingleton<IDevice>(_ => device.Value);
        return services;
    }
}
