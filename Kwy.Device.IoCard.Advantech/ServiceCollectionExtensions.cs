using Kwy.Device.IoCard.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Kwy.Device.Abstractions;
using Kwy.Device.IoCard.Core;

namespace Kwy.Device.IoCard.Advantech;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdvantechIoCard(
        this IServiceCollection services,
        Action<AdvantechIoCardConfig>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new AdvantechIoCardConfig();
        configure?.Invoke(config);

        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Advantech IO card configuration.", nameof(configure));
        }

        services.AddIoCardCore();
        var device = new Lazy<AdvantechIoCardDevice>(() => new AdvantechIoCardDevice(config));
        services.AddSingleton(_ => device.Value);
        services.AddSingleton<IIoCardDevice>(_ => device.Value);
        services.AddSingleton<IDevice>(_ => device.Value);

        return services;
    }
}
