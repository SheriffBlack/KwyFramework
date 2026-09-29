using Kwy.Device.Camera.Abstractions;
using Kwy.Device.Camera.Core;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Camera.HikVision;

public static class ServiceCollectionExtensions
{
    /// <summary>注册一台海康相机；多相机设备可重复调用。</summary>
    public static IServiceCollection AddHikVisionCamera(
        this IServiceCollection services,
        Action<HikCameraConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new HikCameraConfig();
        configure(config);
        config.ValidateAndThrow();

        services.AddCameraCore();
        var device = new Lazy<HikCameraDevice>(() => new HikCameraDevice(config));
        services.AddSingleton(_ => device.Value);
        services.AddSingleton<ICameraDevice>(_ => device.Value);
        services.AddSingleton<IDevice>(_ => device.Value);
        return services;
    }
}
