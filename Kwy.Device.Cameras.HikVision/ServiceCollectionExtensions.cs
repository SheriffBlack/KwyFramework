using Kwy.Device.Abstractions.Vision;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.Cameras.HikVision;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers one HikVision camera. Call repeatedly for multiple cameras.</summary>
    public static IServiceCollection AddKwyHikVisionCamera(
        this IServiceCollection services,
        Action<HikCameraConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new HikCameraConfig();
        configure(config);
        config.ValidateAndThrow();

        services.AddKwyDeviceCore();
        services.AddSingleton<ICameraDevice>(_ => new HikCameraDevice(config));
        return services;
    }
}
