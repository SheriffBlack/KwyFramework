using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Abstractions.Camera;
using Kwy.Device.Core.PLC;
using Kwy.Device.Core.Camera;
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

    public static IServiceCollection AddCameraServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();
        services.TryAddSingleton<ICameraRegistry, CameraRegistry>();
        return services;
    }

    public static IServiceCollection AddPlcServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();
        services.TryAddSingleton<LogicalPlcService>();
        services.TryAddSingleton<ILogicalPlcReader>(provider => provider.GetRequiredService<LogicalPlcService>());
        services.TryAddSingleton<ILogicalPlcWriter>(provider => provider.GetRequiredService<LogicalPlcService>());
        return services;
    }

    public static IServiceCollection AddPlcPointDefinitions(this IServiceCollection services, IEnumerable<PlcPointDefinition> points)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPlcServices();
        var provider = new PlcPointDefinitionProvider(points?.ToArray() ?? throw new ArgumentNullException(nameof(points)));
        services.AddSingleton<IPlcPointDefinitionProvider>(provider);
        return services;
    }
}