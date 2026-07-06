using Kwy.UI.WPF.Services.FileDialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.UI.WPF;

/// <summary>
/// Dependency injection extensions for Kwy WPF platform services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers basic Kwy WPF platform services.
    /// </summary>
    public static IServiceCollection AddKwyWpfServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFileDialogService, WpfFileDialogService>();

        return services;
    }
}
