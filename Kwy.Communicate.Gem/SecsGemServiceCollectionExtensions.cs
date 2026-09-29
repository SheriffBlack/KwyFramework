using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public static class SecsGemServiceCollectionExtensions
{
    public static IServiceCollection AddKwyGem(
        this IServiceCollection services,
        SecsGemClientConfig config,
        Action<GemDiagnosticsOptions>? configureDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        if (services.Any(static descriptor => descriptor.ServiceType == typeof(KwyGemRegistrationMarker)))
        {
            throw new InvalidOperationException(
                "AddKwyGem supports one container-managed GEM endpoint. " +
                "Use SecsGemClientFactory and an application registry when multiple endpoints are required.");
        }

        SecsGemClientFactory.Validate(config);
        var diagnosticsOptions = new GemDiagnosticsOptions();
        configureDiagnostics?.Invoke(diagnosticsOptions);
        diagnosticsOptions.Validate();

        services.AddSingleton(new KwyGemRegistrationMarker());
        services.AddSingleton(config);
        services.AddSingleton(diagnosticsOptions);
        services.AddOptions<SecsGemOptions>()
            .Configure(options => CopyOptions(SecsGemClientFactory.CreateOptions(config), options));
        services.TryAddSingleton<IGemDiagnostics, TraceGemDiagnostics>();
        services.TryAddSingleton<ISecsGemLogger>(provider =>
            new SecsGemLoggerAdapter(
                provider.GetRequiredService<IGemDiagnostics>(),
                provider.GetRequiredService<GemDiagnosticsOptions>()));
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

    private sealed class KwyGemRegistrationMarker;

    private static void CopyOptions(SecsGemOptions source, SecsGemOptions target)
    {
        target.DeviceId = source.DeviceId;
        target.IsActive = source.IsActive;
        target.IpAddress = source.IpAddress;
        target.Port = source.Port;
        target.T3 = source.T3;
        target.T5 = source.T5;
        target.T6 = source.T6;
        target.T7 = source.T7;
        target.T8 = source.T8;
        target.LinkTestInterval = source.LinkTestInterval;
    }
}
