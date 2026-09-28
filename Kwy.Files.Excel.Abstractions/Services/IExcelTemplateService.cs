using Kwy.Files.Excel.Abstractions.Options;

namespace Kwy.Files.Excel.Abstractions.Services;

/// <summary>
/// Template-oriented Excel operations.
/// </summary>
public interface IExcelTemplateService
{
    Task CopySheetFromTemplateAsync(ExcelTemplateCopyOptions options, CancellationToken cancellationToken = default);
}
