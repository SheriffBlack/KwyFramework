namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Template-oriented Excel operations.
/// </summary>
public interface IExcelTemplateService
{
    Task CopySheetFromTemplateAsync(ExcelTemplateCopyOptions options, CancellationToken cancellationToken = default);
}
