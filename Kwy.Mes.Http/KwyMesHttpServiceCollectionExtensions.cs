using Kwy.Mes.Abstractions;
using Kwy.Mes.Http.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Mes.Http;

public static class KwyMesHttpServiceCollectionExtensions
{
    public static IServiceCollection AddKwyHttpMes(this IServiceCollection services, HttpMesOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IHttpMesMessageMapper, JsonHttpMesMessageMapper>();
        services.AddSingleton<IMesService, HttpMesService>();
        return services;
    }
}
