using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Data.EFCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyEfCore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IEfCoreSqlBridge<TContext>, EfCoreSqlBridge<TContext>>();
        return services;
    }
}
