using Kwy.Licensing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Licensing.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyLicensing(
        this IServiceCollection services,
        Action<FeatureLicenseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FeatureLicenseOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IFeatureLicenseService, FeatureLicenseService>();

        return services;
    }
}
