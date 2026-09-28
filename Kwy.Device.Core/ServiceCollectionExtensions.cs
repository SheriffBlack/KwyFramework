using Kwy.Device.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Core;

/// <summary>跨设备领域的基础注册入口。</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDeviceRegistry, DeviceRegistry>();
        return services;
    }

}
