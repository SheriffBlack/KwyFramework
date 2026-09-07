using Kwy.Files.Excel.Abstractions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Kwy.Files.Excel.NPOI;

internal sealed class NpoiExcelWorkbookSession : IExcelWorkbookSession
{
    private readonly IWorkbook workbook;
    private readonly bool readOnly;
    private bool disposed;

    public NpoiExcelWorkbookSession(IWorkbook workbook, string? filePath, ExcelFileFormat format, bool readOnly)
    {
        this.workbook = workbook;
        FilePath = filePath;
        Format = format;
        this.readOnly = readOnly;
    }

    public string? FilePath { get; }

    public ExcelFileFormat Format { get; }

    public Task<IReadOnlyList<string>> GetSheetNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = Enumerable.Range(0, workbook.NumberOfSheets)
            .Select(workbook.GetSheetName)
            .ToArray();
        return Task.FromResult((IReadOnlyList<string>)names);
    }

    public Task<ExcelSheetData> ReadSheetAsync(ExcelReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var sheet = ResolveSheet(options.SheetName);
        var rows = new List<IReadOnlyList<object?>>();
        int startRow = options.StartRow - 1;
        int endRow = options.RowCount.HasValue
            ? Math.Min(sheet.LastRowNum, startRow + options.RowCount.Value - 1)
            : sheet.LastRowNum;

        for (int rowIndex = startRow; rowIndex <= endRow; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = sheet.GetRow(rowIndex);
            int startColumn = options.StartColumn - 1;
            int endColumn = options.ColumnCount.HasValue
                ? startColumn + options.ColumnCount.Value - 1
                : Math.Max(startColumn, (row?.LastCellNum ?? 0) - 1);

            var values = new object?[Math.Max(0, endColumn - startColumn + 1)];
            for (int columnIndex = startColumn; columnIndex <= endColumn; columnIndex++)
            {
                values[columnIndex - startColumn] = NpoiExcelValueConverter.GetValue(row?.GetCell(columnIndex), options.UseFormattedText);
            }

            rows.Add(values);
        }

        return Task.FromResult(new ExcelSheetData(sheet.SheetName, rows));
    }

    public Task<object?> ReadCellAsync(string sheetName, ExcelCellAddress address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sheet = ResolveSheet(sheetName);
        var cell = sheet.GetRow(address.Row - 1)?.GetCell(address.Column - 1);
        return Task.FromResult(NpoiExcelValueConverter.GetValue(cell, formatted: false));
    }

    public Task WriteCellAsync(string sheetName, ExcelCellAddress address, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        cancellationToken.ThrowIfCancellationRequested();

        var sheet = ResolveSheet(sheetName, createIfMissing: true);
        var row = sheet.GetRow(address.Row - 1) ?? sheet.CreateRow(address.Row - 1);
        var cell = row.GetCell(address.Column - 1) ?? row.CreateCell(address.Column - 1);
        NpoiExcelValueConverter.SetValue(cell, value);
        return Task.CompletedTask;
    }

    public Task WriteRangeAsync(ExcelWriteOptions options, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);
        options.Validate();

        var sheet = ResolveSheet(options.SheetName, options.CreateSheetIfMissing);
        for (int rowOffset = 0; rowOffset < values.Count; rowOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = sheet.GetRow(options.StartRow - 1 + rowOffset) ?? sheet.CreateRow(options.StartRow - 1 + rowOffset);
            for (int columnOffset = 0; columnOffset < values[rowOffset].Count; columnOffset++)
            {
                var cell = row.GetCell(options.StartColumn - 1 + columnOffset) ?? row.CreateCell(options.StartColumn - 1 + columnOffset);
                NpoiExcelValueConverter.SetValue(cell, values[rowOffset][columnOffset]);
            }
        }

        if (options.AutoFitColumns && values.Count > 0)
        {
            int columnCount = values.Max(row => row.Count);
            for (int columnOffset = 0; columnOffset < columnCount; columnOffset++)
            {
                sheet.AutoSizeColumn(options.StartColumn - 1 + columnOffset);
            }
        }

        return Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException("The workbook has no file path. Use SaveAsAsync instead.");
        }

        await SaveCoreAsync(FilePath, overwrite: true, cancellationToken);
    }

    public Task SaveAsAsync(ExcelSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return SaveCoreAsync(options.FilePath, options.Overwrite, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            workbook.Close();
        }

        return ValueTask.CompletedTask;
    }

    private async Task SaveCoreAsync(string filePath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && File.Exists(filePath))
        {
            throw new IOException($"File already exists: {filePath}");
        }

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        workbook.Write(stream, leaveOpen: true);
        await stream.FlushAsync(cancellationToken);
    }

    private ISheet ResolveSheet(string? sheetName, bool createIfMissing = false)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            return workbook.GetSheetAt(0);
        }

        var sheet = workbook.GetSheet(sheetName);
        if (sheet != null)
        {
            return sheet;
        }

        if (createIfMissing)
        {
            return workbook.CreateSheet(sheetName);
        }

        throw new ArgumentException($"Worksheet does not exist: {sheetName}", nameof(sheetName));
    }

    private void ThrowIfReadOnly()
    {
        if (readOnly)
        {
            throw new InvalidOperationException("Workbook session is read-only.");
        }
    }

    public static IWorkbook CreateWorkbook(ExcelFileFormat format)
    {
        return format == ExcelFileFormat.Xls ? new HSSFWorkbook() : new XSSFWorkbook();
    }
}
