using Kwy.Mes.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Mes.Core;

public static class KwyMesCoreServiceCollectionExtensions
{
    public static IServiceCollection AddKwyMesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    public static IServiceCollection AddSimulationMes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMesService, SimulationMesService>();
        return services;
    }
}
