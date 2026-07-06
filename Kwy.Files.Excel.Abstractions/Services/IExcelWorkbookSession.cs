namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Represents an opened workbook session owned by an Excel provider.
/// </summary>
public interface IExcelWorkbookSession : IAsyncDisposable
{
    string? FilePath { get; }

    ExcelFileFormat Format { get; }

    Task<IReadOnlyList<string>> GetSheetNamesAsync(CancellationToken cancellationToken = default);

    Task<ExcelSheetData> ReadSheetAsync(ExcelReadOptions options, CancellationToken cancellationToken = default);

    Task<object?> ReadCellAsync(string sheetName, ExcelCellAddress address, CancellationToken cancellationToken = default);

    Task WriteCellAsync(string sheetName, ExcelCellAddress address, object? value, CancellationToken cancellationToken = default);

    Task WriteRangeAsync(ExcelWriteOptions options, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task SaveAsAsync(ExcelSaveOptions options, CancellationToken cancellationToken = default);
}
