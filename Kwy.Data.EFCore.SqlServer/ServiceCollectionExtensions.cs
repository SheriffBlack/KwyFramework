using Kwy.Data.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Data.EFCore.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyEfCoreSqlServer<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddKwyEfCore<TContext>();
        services.AddDbContextFactory<TContext>(builder =>
        {
            builder.UseSqlServer(connectionString);
            configure?.Invoke(builder);
        });

        return services;
    }
}
