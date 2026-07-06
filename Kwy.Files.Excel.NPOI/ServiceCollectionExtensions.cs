using Kwy.Files.Excel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Files.Excel.NPOI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyExcelNpoi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExcelWorkbookService, NpoiExcelWorkbookService>();
        services.TryAddSingleton<IExcelSheetMergeService, NpoiExcelSheetMergeService>();

        return services;
    }
}
