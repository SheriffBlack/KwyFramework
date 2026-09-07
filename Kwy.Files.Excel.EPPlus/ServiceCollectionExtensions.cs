using Kwy.Files.Excel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Files.Excel.EPPlus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyExcelEpplus(
        this IServiceCollection services,
        Action<ExcelEpplusOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ExcelEpplusOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IExcelWorkbookService, EpplusExcelWorkbookService>();
        services.TryAddSingleton<IExcelTemplateService, EpplusExcelTemplateService>();
        services.TryAddSingleton<IExcelSheetMergeService, EpplusExcelSheetMergeService>();

        return services;
    }
}
