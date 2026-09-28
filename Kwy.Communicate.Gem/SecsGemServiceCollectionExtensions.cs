using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public static class SecsGemServiceCollectionExtensions
{
    public static IServiceCollection AddKwyGem(
        this IServiceCollection services,
        SecsGemClientConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.Validate())
        {
            throw new ArgumentException("The SECS/HSMS configuration is invalid.", nameof(config));
        }

        services.AddSingleton(config);
        services.AddOptions<SecsGemOptions>().Configure(options => MapOptions(config, options));
        services.TryAddSingleton<ISecsGemLogger, TraceSecsGemLogger>();
        services.TryAddSingleton<ISecsConnection>(provider =>
        {
            var connection = new HsmsConnection(
                provider.GetRequiredService<IOptions<SecsGemOptions>>(),
                provider.GetRequiredService<ISecsGemLogger>())
            {
                LinkTestEnabled = config.KeepAlive
            };
            return connection;
        });
        services.TryAddSingleton<ISecsGem>(provider =>
            new SecsGem(
                provider.GetRequiredService<IOptions<SecsGemOptions>>(),
                provider.GetRequiredService<ISecsConnection>(),
                provider.GetRequiredService<ISecsGemLogger>()));
        services.TryAddSingleton<ISecsGemClient, SecsGemClient>();
        services.TryAddSingleton<GemRegistry>();
        services.TryAddSingleton<IGemEquipment, GemEquipmentService>();

        return services;
    }

    private static void MapOptions(SecsGemClientConfig source, SecsGemOptions target)
    {
        target.DeviceId = source.DeviceId;
        target.IsActive = source.IsActive;
        target.IpAddress = source.Host;
        target.Port = source.Port;
        target.T3 = source.T3Timeout;
        target.T5 = source.T5Timeout;
        target.T6 = source.T6Timeout;
        target.T7 = source.T7Timeout;
        target.T8 = source.T8Timeout;
        target.LinkTestInterval = source.KeepAliveInterval;
    }

    private sealed class TraceSecsGemLogger : ISecsGemLogger;
}
