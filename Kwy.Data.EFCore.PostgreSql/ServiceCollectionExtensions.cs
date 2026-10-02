using Kwy.Data.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Kwy.Data.EFCore.PostgreSql;

/// <summary>提供 PostgreSQL EF Core Provider 的依赖注入注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>使用 Npgsql EF Core Provider 注册 DbContext 工厂和 Kwy SQL 桥接。</summary>
    /// <typeparam name="TContext">业务 PostgreSQL DbContext 类型。</typeparam>
    /// <param name="services">应用服务集合。</param>
    /// <param name="connectionString">PostgreSQL 连接字符串。</param>
    /// <param name="configure">通用 DbContext 配置。</param>
    /// <param name="configurePostgreSql">Npgsql Provider 专属配置，例如瞬时故障重试。</param>
    /// <returns>便于链式注册的原服务集合。</returns>
    public static IServiceCollection AddKwyEfCorePostgreSql<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null,
        Action<NpgsqlDbContextOptionsBuilder>? configurePostgreSql = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddKwyEfCore<TContext>();
        services.AddDbContextFactory<TContext>(builder =>
        {
            builder.UseNpgsql(connectionString, options => configurePostgreSql?.Invoke(options));
            configure?.Invoke(builder);
        });
        return services;
    }
}
