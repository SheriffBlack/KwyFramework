using Kwy.Device.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Core;

/// <summary>注册跨设备领域的基础运行时服务。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册设备实例注册表。
    /// 各领域 Core 的注册入口应调用本方法，但领域服务必须在各自 <c>*.Core</c> 项目中注册。
    /// </summary>
    public static IServiceCollection AddDeviceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDeviceRegistry, DeviceRegistry>();
        return services;
    }

}
