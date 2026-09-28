using Kwy.Device.Camera.Abstractions.Camera;
using Kwy.Device.Camera.Core;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddCameraServices();
        services.AddSingleton<ICameraDevice>(_ => new HikCameraDevice(config));
        return services;
    }
}
