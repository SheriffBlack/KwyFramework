using Kwy.Device.Abstractions.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Device.IoCards.Advantech;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyAdvantechIoCard(
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

        var device = new Lazy<AdvantechIoCardDevice>(() => new AdvantechIoCardDevice(config));
        services.AddSingleton(_ => device.Value);
        services.AddSingleton<IIoCardDevice>(_ => device.Value);

        return services;
    }
}
