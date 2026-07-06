using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.MVVM.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyMessageBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMessageDispatcher, InlineMessageDispatcher>();
        services.TryAddSingleton<IMessageBus, MessageBus>();
        return services;
    }
}
