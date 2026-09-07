using Kwy.Communicate.Gem;
using Kwy.Device.Abstractions.Equipment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Device.Semiconductor.Gem;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyDeviceGemBridge(
        this IServiceCollection services,
        Action<GemEquipmentBridgeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new GemEquipmentBridgeOptions();
        configure?.Invoke(options);

        services.RemoveAll<GemEquipmentBridgeOptions>();
        services.AddSingleton(options);
        services.TryAddSingleton<IEquipmentGemMapper, DefaultEquipmentGemMapper>();
        services.TryAddSingleton<GemRegistry>();
        services.TryAddSingleton<GemEquipmentBridge>(serviceProvider =>
            new GemEquipmentBridge(
                serviceProvider.GetRequiredService<IGemEquipment>(),
                serviceProvider.GetRequiredService<GemRegistry>(),
                serviceProvider.GetRequiredService<IEquipmentGemMapper>(),
                serviceProvider.GetRequiredService<GemEquipmentBridgeOptions>(),
                serviceProvider.GetService<IEquipmentStateMachine>()));

        services.TryAddSingleton<IGemEquipmentBridge>(serviceProvider =>
            serviceProvider.GetRequiredService<GemEquipmentBridge>());

        if (options.RegisterAsPrimaryEventSink)
        {
            services.RemoveAll<IEquipmentEventSink>();
            services.AddSingleton<IEquipmentEventSink>(serviceProvider =>
                serviceProvider.GetRequiredService<GemEquipmentBridge>());
        }

        return services;
    }
}
