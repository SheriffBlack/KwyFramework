using Kwy.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Logging.Serilog;

/// <summary>
/// Dependency injection extensions for Kwy Serilog logging.
/// </summary>
public static class KwySerilogServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kwy logging service backed by Serilog.
    /// </summary>
    public static IServiceCollection AddKwySerilogLogging(
        this IServiceCollection services,
        Action<KwyLoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KwyLoggingOptions();
        configure?.Invoke(options);
        Validate(options);

        services.AddSingleton(options);
        services.AddSingleton<ILogService, SerilogLogService>();

        return services;
    }

    private static void Validate(KwyLoggingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            throw new ArgumentException("Log directory cannot be empty.", nameof(options));
        }

    }
}
