using Kwy.Data.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Data.EFCore.Sqlite;

public static class ServiceCollectionExtensions
{
    private static int sqliteInitialized;

    public static IServiceCollection AddKwyEfCoreSqlite<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        EnsureSqliteInitialized();

        services.AddKwyEfCore<TContext>();
        services.AddDbContextFactory<TContext>(builder =>
        {
            builder.UseSqlite(connectionString);
            configure?.Invoke(builder);
        });

        return services;
    }

    private static void EnsureSqliteInitialized()
    {
        if (Interlocked.Exchange(ref sqliteInitialized, 1) == 1)
        {
            return;
        }

        SQLitePCL.Batteries_V2.Init();
    }
}
