using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Vision.Halcon.WPF;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers HALCON WPF rendering services.
    /// The current package exposes the HALCON smart-window viewer control and keeps registration lightweight.
    /// </summary>
    public static IServiceCollection AddKwyHalconWpfRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
