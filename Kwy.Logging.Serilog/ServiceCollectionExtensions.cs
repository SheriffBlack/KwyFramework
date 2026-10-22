using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Kwy.Logging.Serilog;

/// <summary>Serilog 日志的依赖注入扩展。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>将 Serilog 注册为 Microsoft.Extensions.Logging 的日志提供程序。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">日志配置委托。</param>
    /// <returns>当前服务集合，用于链式调用。</returns>
    public static IServiceCollection AddSerilogLogging(
        this IServiceCollection services,
        Action<LoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LoggingOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddLogging(builder => builder.AddSerilog(
            LoggingConfig.CreateLogger(options),
            dispose: true));

        return services;
    }
}
