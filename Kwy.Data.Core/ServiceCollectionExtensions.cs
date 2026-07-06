using Kwy.Data.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Data.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyDataCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDatabaseTransactionFactory, DatabaseTransactionFactory>();

        return services;
    }
}
