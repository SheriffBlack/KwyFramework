using Kwy.Device.Camera.Abstractions;
using Kwy.Device.Camera.Core;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Camera.Core;

/// <summary>注册相机领域服务。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册相机注册表；厂商适配器可继续注册一个或多个 <see cref="ICameraDevice"/>。</summary>
    public static IServiceCollection AddCameraCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();
        services.TryAddSingleton<ICameraRegistry, CameraRegistry>();
        return services;
    }
}
