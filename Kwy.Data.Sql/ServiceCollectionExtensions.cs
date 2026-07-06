using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Data.Sql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwySql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISqlExecutor, DbCommandSqlExecutor>();
        return services;
    }
}
