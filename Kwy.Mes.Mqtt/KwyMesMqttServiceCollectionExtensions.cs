using Kwy.Mes.Abstractions;
using Kwy.Mes.Mqtt.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Mes.Mqtt;

public static class KwyMesMqttServiceCollectionExtensions
{
    public static IServiceCollection AddKwyMqttMes(this IServiceCollection services, MqttMesOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IMqttMesMessageMapper, JsonMqttMesMessageMapper>();
        services.AddSingleton<IMesService, MqttMesService>();
        return services;
    }
}
