using Kwy.Data.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Data.Core;

/// <summary>提供 Kwy 数据库核心服务的依赖注入注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册命名数据源解析器和事务工厂。
    /// </summary>
    /// <remarks>
    /// 具体 Provider 的 AddKwyXxx 扩展应调用此方法；TryAdd 语义允许应用替换默认实现。
    /// </remarks>
    /// <param name="services">应用服务集合。</param>
    /// <returns>便于链式注册的原服务集合。</returns>
    public static IServiceCollection AddKwyDataCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDatabaseTransactionFactory, DatabaseTransactionFactory>();
        services.TryAddSingleton<IDatabaseConnectionFactoryResolver, DatabaseConnectionFactoryResolver>();

        return services;
    }
}
