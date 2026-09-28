using Kwy.Files.Excel.Abstractions.Models;
using Kwy.Files.Excel.Abstractions.Options;

namespace Kwy.Files.Excel.Abstractions.Services;

/// <summary>
/// High-level workbook operations.
/// </summary>
public interface IExcelWorkbookService
{
    ExcelProviderInfo ProviderInfo { get; }

    Task<IExcelWorkbookSession> OpenAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);

    Task<IExcelWorkbookSession> CreateAsync(ExcelFileFormat format = ExcelFileFormat.Xlsx, CancellationToken cancellationToken = default);

    Task<ExcelWorkbookData> ReadWorkbookAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSheetNamesAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default);
}
