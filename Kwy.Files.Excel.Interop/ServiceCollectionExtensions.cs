using Kwy.Files.Excel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Files.Excel.Interop;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyExcelInterop(
        this IServiceCollection services,
        Action<ExcelInteropOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ExcelInteropOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IExcelInteropEnvironment, ExcelInteropEnvironment>();
        services.TryAddSingleton<ExcelInteropApplication>();
        services.TryAddSingleton<IExcelWorkbookService, ExcelInteropWorkbookService>();
        services.TryAddSingleton<IExcelSheetMergeService, ExcelInteropSheetMergeService>();

        return services;
    }
}
