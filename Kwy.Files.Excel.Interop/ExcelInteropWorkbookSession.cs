using Kwy.Files.Excel.Abstractions;
using Kwy.Files.Excel.Interop.Interop;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

internal sealed class ExcelInteropWorkbookSession : IExcelWorkbookSession
{
    private readonly ExcelInteropApplication application;
    private readonly OfficeExcel.Workbook workbook;
    private readonly bool readOnly;
    private bool disposed;

    public ExcelInteropWorkbookSession(
        ExcelInteropApplication application,
        OfficeExcel.Workbook workbook,
        string? filePath,
        ExcelFileFormat format,
        bool readOnly)
    {
        this.application = application;
        this.workbook = workbook;
        FilePath = filePath;
        Format = format;
        this.readOnly = readOnly;
    }

    public string? FilePath { get; }

    public ExcelFileFormat Format { get; }

    public Task<IReadOnlyList<string>> GetSheetNamesAsync(CancellationToken cancellationToken = default)
    {
        return application.RunAsync(_ =>
        {
            var names = new List<string>();
            OfficeExcel.Sheets? worksheets = null;
            try
            {
                worksheets = workbook.Worksheets;
                foreach (OfficeExcel.Worksheet worksheet in worksheets)
                {
                    names.Add(worksheet.Name);
                    ComObject.Release(worksheet);
                }
            }
            finally
            {
                ComObject.Release(worksheets);
            }

            return (IReadOnlyList<string>)names;
        }, cancellationToken);
    }

    public Task<ExcelSheetData> ReadSheetAsync(ExcelReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return application.RunAsync(_ =>
        {
            OfficeExcel.Worksheet? worksheet = null;
            OfficeExcel.Range? usedRange = null;
            try
            {
                worksheet = ResolveWorksheet(options.SheetName);
                usedRange = worksheet.UsedRange;
                object? values = usedRange.Value2;

                var rows = ConvertRangeValues(values, options);
                return new ExcelSheetData(worksheet.Name, rows);
            }
            finally
            {
                ComObject.Release(usedRange);
                ComObject.Release(worksheet);
            }
        }, cancellationToken);
    }

    public Task<object?> ReadCellAsync(string sheetName, ExcelCellAddress address, CancellationToken cancellationToken = default)
    {
        return application.RunAsync<object?>(_ =>
        {
            OfficeExcel.Worksheet? worksheet = null;
            OfficeExcel.Range? cell = null;
            try
            {
                worksheet = ResolveWorksheet(sheetName);
                cell = (OfficeExcel.Range)worksheet.Cells[address.Row, address.Column];
                return cell.Value2;
            }
            finally
            {
                ComObject.Release(cell);
                ComObject.Release(worksheet);
            }
        }, cancellationToken);
    }

    public Task WriteCellAsync(string sheetName, ExcelCellAddress address, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        return application.RunAsync(_ =>
        {
            OfficeExcel.Worksheet? worksheet = null;
            OfficeExcel.Range? cell = null;
            try
            {
                worksheet = ResolveWorksheet(sheetName);
                cell = (OfficeExcel.Range)worksheet.Cells[address.Row, address.Column];
                cell.Value2 = value;
            }
            finally
            {
                ComObject.Release(cell);
                ComObject.Release(worksheet);
            }
        }, cancellationToken);
    }

    public Task WriteRangeAsync(ExcelWriteOptions options, IReadOnlyList<IReadOnlyList<object?>> values, CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(values);
        options.Validate();

        return application.RunAsync(_ =>
        {
            OfficeExcel.Worksheet? worksheet = null;
            OfficeExcel.Range? destination = null;
            OfficeExcel.Range? startCell = null;
            OfficeExcel.Range? endCell = null;
            OfficeExcel.Range? columns = null;
            try
            {
                worksheet = ResolveWorksheet(options.SheetName, options.CreateSheetIfMissing);
                var matrix = ToMatrix(values);
                int rowCount = matrix.GetLength(0);
                int columnCount = matrix.GetLength(1);
                if (rowCount == 0 || columnCount == 0)
                {
                    return;
                }

                startCell = (OfficeExcel.Range)worksheet.Cells[options.StartRow, options.StartColumn];
                endCell = (OfficeExcel.Range)worksheet.Cells[options.StartRow + rowCount - 1, options.StartColumn + columnCount - 1];
                destination = worksheet.Range[startCell, endCell];
                destination.Value2 = matrix;

                if (options.AutoFitColumns)
                {
                    columns = destination.Columns;
                    columns.AutoFit();
                }
            }
            finally
            {
                ComObject.Release(columns);
                ComObject.Release(destination);
                ComObject.Release(endCell);
                ComObject.Release(startCell);
                ComObject.Release(worksheet);
            }
        }, cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfReadOnly();
        return application.RunAsync(_ => workbook.Save(), cancellationToken);
    }

    public Task SaveAsAsync(ExcelSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return application.RunAsync(_ =>
        {
            if (!options.Overwrite && File.Exists(options.FilePath))
            {
                throw new IOException($"File already exists: {options.FilePath}");
            }

            var format = ExcelInteropConverters.NormalizeFormat(options.FilePath, options.Format);
            workbook.SaveAs(
                options.FilePath,
                ExcelInteropConverters.ToExcelFileFormat(format),
                ExcelInteropConverters.ToMissingIfNull(options.Password));
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await application.RunAsync(_ =>
        {
            try
            {
                workbook.Close(SaveChanges: false);
            }
            finally
            {
                ComObject.Release(workbook);
            }
        });
    }

    private OfficeExcel.Worksheet ResolveWorksheet(string? sheetName, bool createIfMissing = false)
    {
        OfficeExcel.Sheets? worksheets = null;
        try
        {
            worksheets = workbook.Worksheets;

        if (!string.IsNullOrWhiteSpace(sheetName))
        {
            foreach (OfficeExcel.Worksheet worksheet in worksheets)
            {
                if (string.Equals(worksheet.Name, sheetName, StringComparison.Ordinal))
                {
                    return worksheet;
                }

                ComObject.Release(worksheet);
            }

            if (createIfMissing)
            {
                var after = worksheets[worksheets.Count];
                var created = (OfficeExcel.Worksheet)worksheets.Add(After: after);
                ComObject.Release(after);
                created.Name = sheetName;
                return created;
            }

            throw new ArgumentException($"Worksheet does not exist: {sheetName}", nameof(sheetName));
        }

            return (OfficeExcel.Worksheet)worksheets[1];
        }
        finally
        {
            ComObject.Release(worksheets);
        }
    }

    private void ThrowIfReadOnly()
    {
        if (readOnly)
        {
            throw new InvalidOperationException("Workbook session is read-only.");
        }
    }

    private static IReadOnlyList<IReadOnlyList<object?>> ConvertRangeValues(object? values, ExcelReadOptions options)
    {
        if (values == null)
        {
            return Array.Empty<IReadOnlyList<object?>>();
        }

        if (values is not object[,] matrix)
        {
            return new[] { new object?[] { values } };
        }

        int totalRows = matrix.GetUpperBound(0);
        int totalColumns = matrix.GetUpperBound(1);
        int startRow = Math.Min(options.StartRow, totalRows);
        int startColumn = Math.Min(options.StartColumn, totalColumns);
        int endRow = options.RowCount.HasValue ? Math.Min(totalRows, startRow + options.RowCount.Value - 1) : totalRows;
        int endColumn = options.ColumnCount.HasValue ? Math.Min(totalColumns, startColumn + options.ColumnCount.Value - 1) : totalColumns;

        var rows = new List<IReadOnlyList<object?>>();
        for (int row = startRow; row <= endRow; row++)
        {
            var current = new object?[endColumn - startColumn + 1];
            for (int column = startColumn; column <= endColumn; column++)
            {
                current[column - startColumn] = matrix[row, column];
            }

            rows.Add(current);
        }

        return rows;
    }

    private static object?[,] ToMatrix(IReadOnlyList<IReadOnlyList<object?>> values)
    {
        int rowCount = values.Count;
        int columnCount = rowCount == 0 ? 0 : values.Max(row => row.Count);
        var matrix = new object?[rowCount, columnCount];

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < values[row].Count; column++)
            {
                matrix[row, column] = values[row][column];
            }
        }

        return matrix;
    }
}
