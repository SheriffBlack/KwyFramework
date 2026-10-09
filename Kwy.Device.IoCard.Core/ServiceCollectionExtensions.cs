using Kwy.Device.IoCard.Abstractions;
using Kwy.Device.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kwy.Device.IoCard.Core;

namespace Kwy.Device.IoCard.Core;

/// <summary>注册 IO 领域运行时服务；由设备组合根调用，不应由业务流程直接构造。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册逻辑 IO 点位监视、读写与安全输出服务。</summary>
    public static IServiceCollection AddIoCardCore(
        this IServiceCollection services,
        Action<DigitalIoStateMonitorOptions>? configureIoMonitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDeviceCore();

        var options = new DigitalIoStateMonitorOptions();
        configureIoMonitor?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton<DigitalIoStateMonitor>();
        services.TryAddSingleton<IDigitalIoStateMonitor>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<ILogicalDigitalInputReader>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<ILogicalDigitalOutputWriter>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<IDigitalOutputSafeStateController>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<IDigitalIoStateSubscription>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<ILogicalDigitalInputInterruptWaiter>(provider => provider.GetRequiredService<DigitalIoStateMonitor>());
        services.TryAddSingleton<LogicalAnalogIoService>();
        services.TryAddSingleton<ILogicalAnalogInputReader>(provider => provider.GetRequiredService<LogicalAnalogIoService>());
        services.TryAddSingleton<ILogicalAnalogOutputWriter>(provider => provider.GetRequiredService<LogicalAnalogIoService>());
        services.TryAddSingleton<IAnalogOutputSafeStateController>(provider => provider.GetRequiredService<LogicalAnalogIoService>());
        return services;
    }

    /// <summary>注册设备统一 IO 点位定义；连接完成后由监视器校验实际设备和通道。</summary>
    public static IServiceCollection AddDigitalIoDefinitions(
        this IServiceCollection services,
        IEnumerable<DigitalIoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIoCardCore();
        DigitalIoPointDefinition[] items = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        services.AddSingleton<DigitalIoPointDefinitionProvider>(_ => new DigitalIoPointDefinitionProvider(items));
        services.AddSingleton<IDigitalIoPointDefinitionProvider>(provider => provider.GetRequiredService<DigitalIoPointDefinitionProvider>());
        return services;
    }

    /// <summary>注册模拟量 IO 点位定义及按稳定 ID 读写的逻辑服务。</summary>
    public static IServiceCollection AddAnalogIoDefinitions(
        this IServiceCollection services,
        IEnumerable<AnalogIoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIoCardCore();
        AnalogIoPointDefinition[] items = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
        services.AddSingleton<AnalogIoPointDefinitionProvider>(_ => new AnalogIoPointDefinitionProvider(items));
        services.AddSingleton<IAnalogIoPointDefinitionProvider>(provider => provider.GetRequiredService<AnalogIoPointDefinitionProvider>());
        return services;
    }
}
