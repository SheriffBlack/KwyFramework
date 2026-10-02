using Kwy.Data.Abstractions;
using Kwy.Data.Core;
using Kwy.Data.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Data.Sql.PostgreSql;

/// <summary>提供 PostgreSQL 原生 SQL 数据源的依赖注入注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注册一个使用 Npgsql 的命名 PostgreSQL 数据源。</summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="connectionString">PostgreSQL 连接字符串。</param>
    /// <param name="dataSourceName">数据源逻辑名称，例如 Historian。</param>
    /// <param name="configure">数据源默认超时等配置。</param>
    /// <returns>便于链式注册的原服务集合。</returns>
    public static IServiceCollection AddKwyPostgreSql(
        this IServiceCollection services,
        string connectionString,
        string dataSourceName = KwyDataSourceNames.Default,
        Action<KwyDataSourceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceName);

        var options = new KwyDataSourceOptions
        {
            Name = dataSourceName,
            Provider = KwyDatabaseProvider.PostgreSql,
            ConnectionString = connectionString
        };
        configure?.Invoke(options);
        options.ValidateAndThrow();
        if (options.Provider != KwyDatabaseProvider.PostgreSql)
        {
            throw new InvalidOperationException("AddKwyPostgreSql cannot register a non-PostgreSql provider.");
        }

        services.AddKwyDataCore();
        services.AddKwySql();
        services.AddSingleton(options);
        services.AddSingleton<IDatabaseConnectionFactory>(new PostgreSqlConnectionFactory(options));
        return services;
    }
}
