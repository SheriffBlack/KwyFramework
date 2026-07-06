using Kwy.Files.Excel.Abstractions;
using OfficeOpenXml;

namespace Kwy.Files.Excel.EPPlus;

internal sealed class EpplusExcelWorkbookSession : IExcelWorkbookSession
{
    private readonly ExcelPackage package;
    private readonly bool readOnly;
    private bool disposed;

    public EpplusExcelWorkbookSession(ExcelPackage package, string? filePath, ExcelFileFormat format, bool readOnly)
    {
        this.package = package;
        FilePath = filePath;
        Format = format;
        this.readOnly = readOnly;
    }

    public string? FilePath { get; }

    public ExcelFileFormat Format { get; }

    public Task<IReadOnlyList<string>> GetSheetNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = package.Workbook.Worksheets.Select(sheet => sheet.Name).ToArray();
        return Task.FromResult((IReadOnlyList<string>)names);
    }

    public Task<ExcelSheetData> ReadSheetAsync(ExcelReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var worksheet = ResolveWorksheet(options.SheetName);
        if (worksheet.Dimension == null)
        {
            return Task.FromResult(new ExcelSheetData(worksheet.Name, Array.Empty<IReadOnlyList<object?>>()));
        }

        int startRow = Math.Max(options.StartRow, worksheet.Dimension.Start.Row);
        int startColumn = Math.Max(options.StartColumn, worksheet.Dimension.Start.Column);
        int endRow = options.RowCount.HasValue
            ? Math.Min(worksheet.Dimension.End.Row, startRow + options.RowCount.Value - 1)
            : worksheet.Dimension.End.Row;
        int endColumn = options.ColumnCount.HasValue
            ? Math.Min(worksheet.Dimension.End.Column, startColumn + options.ColumnCount.Value - 1)
            : worksheet.Dimension.End.Column;

        var rows = new List<IReadOnlyList<object?>>();
        for (int row = startRow; row <= endRow; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new object?[endColumn - startColumn + 1];
            for (int column = startColumn; column <= endColumn; column++)
            {
                var cell = worksheet.Cells[row, column];
                values[column - startColumn] = options.UseFormattedText ? cell.Text : cell.Value;
            }

            rows.Add(values);
        }

        return Task.FromResult(new ExcelSheetData(worksheet.Name, rows));
    }

    public Task<object?> ReadCellAsync(string sheetName, Kwy.Files.Excel.Abstractions.ExcelCellAddress address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<object?>(ResolveWorksheet(sheetName).Cells[address.Row, address.Column].Value);
    }

    public Task WriteCellAsync(string sheetName, Kwy.Files.Excel.Abstractions.ExcelCellAddress address, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        cancellationToken.ThrowIfCancellationRequested();
        ResolveWorksheet(sheetName, createIfMissing: true).Cells[address.Row, address.Column].Value = value;
        return Task.CompletedTask;
    }

    public Task WriteRangeAsync(ExcelWriteOptions options, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);
        options.Validate();

        var worksheet = ResolveWorksheet(options.SheetName, options.CreateSheetIfMissing);
        for (int rowOffset = 0; rowOffset < values.Count; rowOffset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int columnOffset = 0; columnOffset < values[rowOffset].Count; columnOffset++)
            {
                worksheet.Cells[options.StartRow + rowOffset, options.StartColumn + columnOffset].Value = values[rowOffset][columnOffset];
            }
        }

        if (options.AutoFitColumns && worksheet.Dimension != null)
        {
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new InvalidOperationException("The workbook has no file path. Use SaveAsAsync instead.");
        }

        package.Save();
        return Task.CompletedTask;
    }

    public Task SaveAsAsync(ExcelSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Overwrite && File.Exists(options.FilePath))
        {
            throw new IOException($"File already exists: {options.FilePath}");
        }

        package.SaveAs(new FileInfo(options.FilePath), options.Password);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            package.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private ExcelWorksheet ResolveWorksheet(string? sheetName, bool createIfMissing = false)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            return package.Workbook.Worksheets.FirstOrDefault()
                ?? package.Workbook.Worksheets.Add("Sheet1");
        }

        var worksheet = package.Workbook.Worksheets[sheetName];
        if (worksheet != null)
        {
            return worksheet;
        }

        if (createIfMissing)
        {
            return package.Workbook.Worksheets.Add(sheetName);
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
}
