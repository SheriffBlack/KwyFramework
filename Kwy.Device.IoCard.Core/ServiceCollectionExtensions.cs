using Kwy.Device.Io.Abstractions;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Io.Core;

/// <summary>注册 IO 领域运行时服务；由设备组合根调用，不应由业务流程直接构造。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册逻辑 IO 点位监视、读写与安全输出服务。</summary>
    public static IServiceCollection AddIoServices(
        this IServiceCollection services,
        Action<IoStateMonitorOptions>? configureIoMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();

        var options = new IoStateMonitorOptions();
        configureIoMonitor?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton<IoStateMonitor>();
        services.TryAddSingleton<IIoStateMonitor>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoReader>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoWriter>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<IProcessOutputStateController>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<IIoStateSubscription>(provider => provider.GetRequiredService<IoStateMonitor>());
        services.TryAddSingleton<ILogicalIoInterruptWaiter>(provider => provider.GetRequiredService<IoStateMonitor>());
        return services;
    }

    /// <summary>注册设备统一 IO 点位定义；连接完成后由监视器校验实际设备和通道。</summary>
    public static IServiceCollection AddIoPointDefinitions(
        this IServiceCollection services,
        IEnumerable<IoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIoServices();
        IoPointDefinition[] items = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        services.AddSingleton<IoPointDefinitionProvider>(_ => new IoPointDefinitionProvider(items));
        services.AddSingleton<IIoPointDefinitionProvider>(provider => provider.GetRequiredService<IoPointDefinitionProvider>());
        return services;
    }
}
