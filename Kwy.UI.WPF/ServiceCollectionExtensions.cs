using Kwy.UI.Services.FileDialogs;
using Kwy.UI.Threading;
using Kwy.UI.WPF.Services.FileDialogs;
using Kwy.UI.WPF.Threading;
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
        services.TryAddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(System.Windows.Application.Current?.Dispatcher
            ?? System.Windows.Threading.Dispatcher.CurrentDispatcher));

        return services;
    }
}
