using Kwy.Data.Abstractions;
using Kwy.Data.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Data.Sql.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwySqlite(
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
            Provider = KwyDatabaseProvider.Sqlite,
            ConnectionString = connectionString
        };
        configure?.Invoke(options);
        options.ValidateAndThrow();

        services.AddKwyDataCore();
        services.AddKwySql();
        services.AddSingleton(options);
        services.AddSingleton<IDatabaseConnectionFactory, SqliteConnectionFactory>();

        return services;
    }
}
