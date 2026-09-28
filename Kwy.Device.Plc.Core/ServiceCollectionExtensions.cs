using Kwy.Device.Core;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.PLC.Core;

/// <summary>注册 PLC 领域的逻辑点位服务。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册按稳定点位 ID 读写的逻辑 PLC 服务。</summary>
    public static IServiceCollection AddPLCCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();
        services.TryAddSingleton<LogicalPlcService>();
        services.TryAddSingleton<ILogicalPlcReader>(provider => provider.GetRequiredService<LogicalPlcService>());
        services.TryAddSingleton<ILogicalPlcWriter>(provider => provider.GetRequiredService<LogicalPlcService>());
        return services;
    }

    /// <summary>注册 PLC 业务点位定义，并启用逻辑 PLC 服务。</summary>
    public static IServiceCollection AddPLCPointDefinitions(this IServiceCollection services, IEnumerable<PlcPointDefinition> points)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPLCCore();
        var provider = new PlcPointDefinitionProvider(points?.ToArray() ?? throw new ArgumentNullException(nameof(points)));
        services.AddSingleton<IPlcPointDefinitionProvider>(provider);
        return services;
    }
}
