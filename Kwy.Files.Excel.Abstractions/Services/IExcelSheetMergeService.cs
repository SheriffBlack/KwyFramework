namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Merges data from multiple worksheets or files.
/// </summary>
public interface IExcelSheetMergeService
{
    Task<IReadOnlyDictionary<string, ExcelSheetMergeResult>> MergeFilesAsync(
        IEnumerable<string> filePaths,
        ExcelSheetMergeOptions options,
        IProgress<ExcelSheetMergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
