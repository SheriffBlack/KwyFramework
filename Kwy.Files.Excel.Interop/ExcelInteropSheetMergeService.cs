using Kwy.Files.Excel.Abstractions;
using Kwy.Files.Excel.Interop.Interop;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropSheetMergeService : IExcelSheetMergeService
{
    private readonly ExcelInteropApplication application;

    public ExcelInteropSheetMergeService(ExcelInteropApplication application)
    {
        this.application = application;
    }

    public async Task<IReadOnlyDictionary<string, ExcelSheetMergeResult>> MergeFilesAsync(
        IEnumerable<string> filePaths,
        ExcelSheetMergeOptions options,
        IProgress<ExcelSheetMergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(options);

        var files = filePaths.ToArray();
        var results = new Dictionary<string, ExcelSheetMergeResult>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string filePath = files[index];
            var result = await MergeSingleFileAsync(filePath, options, cancellationToken);
            results[filePath] = result;
            progress?.Report(new ExcelSheetMergeProgress(filePath, index + 1, files.Length, result));
        }

        return results;
    }

    private Task<ExcelSheetMergeResult> MergeSingleFileAsync(
        string filePath,
        ExcelSheetMergeOptions options,
        CancellationToken cancellationToken)
    {
        return application.RunAsync(excel =>
        {
            var result = new ExcelSheetMergeResult();
            OfficeExcel.Workbook? workbook = null;
            OfficeExcel.Worksheet? summarySheet = null;

            try
            {
                workbook = excel.Workbooks.Open(filePath, UpdateLinks: 0, ReadOnly: false, IgnoreReadOnlyRecommended: true);
                summarySheet = CreateOrClearSummarySheet(workbook, options.SummarySheetName);

                var stats = MergeData(workbook, summarySheet, options, result);
                result.TotalRows = stats.TotalRows;
                result.ProcessedSheetCount = stats.ProcessedSheetCount;

                int lastColumn = AddExtraColumns(summarySheet, stats.LastColumn, options);
                FormatSummarySheet(excel, summarySheet, result.TotalRows, lastColumn);

                summarySheet.Activate();
                workbook.Save();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        workbook.Close(SaveChanges: false);
                    }
                    catch
                    {
                    }
                }

                ComObject.Release(summarySheet);
                ComObject.Release(workbook);
            }

            return result;
        }, cancellationToken);
    }

    private static (int TotalRows, int ProcessedSheetCount, int LastColumn) MergeData(
        OfficeExcel.Workbook workbook,
        OfficeExcel.Worksheet summarySheet,
        ExcelSheetMergeOptions options,
        ExcelSheetMergeResult result)
    {
        int currentPasteRow = 1;
        int processedCount = 0;
        int maxColumnCount = 0;
        bool isFirstSheet = true;

        foreach (var sheetName in GetSheetNames(workbook))
        {
            if (string.Equals(sheetName, options.SummarySheetName, StringComparison.Ordinal))
            {
                continue;
            }

            OfficeExcel.Worksheet? worksheet = null;
            OfficeExcel.Range? usedRange = null;
            try
            {
                worksheet = (OfficeExcel.Worksheet)workbook.Worksheets[sheetName];
                usedRange = worksheet.UsedRange;
                if (usedRange.Cells.Count == 1 && usedRange.Value2 == null)
                {
                    continue;
                }

                if (usedRange.Value2 is not object[,] rawData)
                {
                    continue;
                }

                int totalRows = rawData.GetUpperBound(0);
                int totalColumns = rawData.GetUpperBound(1);
                if (totalRows < options.StartRow)
                {
                    continue;
                }

                maxColumnCount = Math.Max(maxColumnCount, totalColumns);
                if (options.HeaderContent != null)
                {
                    maxColumnCount = Math.Max(maxColumnCount, options.HeaderContent.Length);
                }

                if (isFirstSheet)
                {
                    int headerColumns = options.HeaderContent != null
                        ? Math.Max(totalColumns, options.HeaderContent.Length)
                        : totalColumns;
                    WriteHeader(summarySheet, rawData, options, headerColumns);
                    currentPasteRow = 2;
                    isFirstSheet = false;
                }

                var filteredData = FilterData(rawData, options);
                if (filteredData == null)
                {
                    processedCount++;
                    continue;
                }

                int writeRows = filteredData.GetLength(0);
                int writeColumns = filteredData.GetLength(1);
                OfficeExcel.Range? destination = null;
                try
                {
                    destination = summarySheet.Range[
                        summarySheet.Cells[currentPasteRow, 1],
                        summarySheet.Cells[currentPasteRow + writeRows - 1, writeColumns]];
                    destination.Value2 = filteredData;
                }
                finally
                {
                    ComObject.Release(destination);
                }

                for (int row = 0; row < writeRows; row++)
                {
                    var values = new object?[writeColumns];
                    for (int column = 0; column < writeColumns; column++)
                    {
                        values[column] = filteredData[row, column];
                    }

                    result.AllRowsData.Add(values);
                }

                currentPasteRow += writeRows;
                processedCount++;
            }
            finally
            {
                ComObject.Release(usedRange);
                ComObject.Release(worksheet);
            }
        }

        return (currentPasteRow - 1, processedCount, maxColumnCount);
    }

    private static IReadOnlyList<string> GetSheetNames(OfficeExcel.Workbook workbook)
    {
        var names = new List<string>();
        foreach (OfficeExcel.Worksheet worksheet in workbook.Worksheets)
        {
            names.Add(worksheet.Name);
            ComObject.Release(worksheet);
        }

        return names;
    }

    private static OfficeExcel.Worksheet CreateOrClearSummarySheet(OfficeExcel.Workbook workbook, string sheetName)
    {
        foreach (OfficeExcel.Worksheet worksheet in workbook.Worksheets)
        {
            if (string.Equals(worksheet.Name, sheetName, StringComparison.Ordinal))
            {
                worksheet.Cells.Clear();
                worksheet.Move(Before: workbook.Worksheets[1]);
                return worksheet;
            }

            ComObject.Release(worksheet);
        }

        var created = (OfficeExcel.Worksheet)workbook.Worksheets.Add(Before: workbook.Worksheets[1]);
        created.Name = sheetName;
        return created;
    }

    private static void WriteHeader(OfficeExcel.Worksheet summarySheet, object[,] rawData, ExcelSheetMergeOptions options, int headerColumns)
    {
        var headerData = new object?[1, headerColumns];
        if (options.HeaderContent != null)
        {
            for (int index = 0; index < headerColumns; index++)
            {
                string header = index < options.HeaderContent.Length ? options.HeaderContent[index] : string.Empty;
                headerData[0, index] = string.IsNullOrWhiteSpace(header) ? new string(' ', index + 1) : header;
            }
        }
        else
        {
            for (int column = 1; column <= headerColumns; column++)
            {
                headerData[0, column - 1] = rawData[options.HeaderRow, column];
            }
        }

        OfficeExcel.Range? range = null;
        try
        {
            range = summarySheet.Range[summarySheet.Cells[1, 1], summarySheet.Cells[1, headerColumns]];
            range.Value2 = headerData;
        }
        finally
        {
            ComObject.Release(range);
        }
    }

    private static object?[,]? FilterData(object[,] source, ExcelSheetMergeOptions options)
    {
        int rows = source.GetUpperBound(0);
        int columns = source.GetUpperBound(1);
        var validRows = new List<object?[]>();

        for (int row = options.StartRow; row <= rows; row++)
        {
            if (ShouldDropRow(source, row, columns, options))
            {
                continue;
            }

            var current = new object?[columns];
            for (int column = 1; column <= columns; column++)
            {
                current[column - 1] = source[row, column];
            }

            validRows.Add(current);
        }

        if (validRows.Count == 0)
        {
            return null;
        }

        var result = new object?[validRows.Count, columns];
        for (int row = 0; row < validRows.Count; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                result[row, column] = validRows[row][column];
            }
        }

        return result;
    }

    private static bool ShouldDropRow(object[,] source, int row, int columns, ExcelSheetMergeOptions options)
    {
        if (options.FilterCheckColumns is { Length: > 0 })
        {
            bool allEmpty = true;
            foreach (int column in options.FilterCheckColumns)
            {
                if (column <= columns && !string.IsNullOrWhiteSpace(source[row, column]?.ToString()))
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty)
            {
                return true;
            }
        }

        if (options.ExcludeRules == null)
        {
            return false;
        }

        foreach (var rule in options.ExcludeRules)
        {
            int column = rule.Key;
            if (column > columns)
            {
                continue;
            }

            string? value = source[row, column]?.ToString()?.Trim();
            if (value == null)
            {
                continue;
            }

            if (rule.Value.Any(excluded => value.Equals(excluded, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static int AddExtraColumns(OfficeExcel.Worksheet summarySheet, int lastColumn, ExcelSheetMergeOptions options)
    {
        if (options.AdditionalColumnsCount <= 0)
        {
            return lastColumn;
        }

        int newLastColumn = lastColumn + options.AdditionalColumnsCount;
        if (options.AdditionalColumnsHeaders == null)
        {
            return newLastColumn;
        }

        var headers = new object?[1, options.AdditionalColumnsCount];
        for (int index = 0; index < options.AdditionalColumnsCount; index++)
        {
            headers[0, index] = index < options.AdditionalColumnsHeaders.Length ? options.AdditionalColumnsHeaders[index] : string.Empty;
        }

        OfficeExcel.Range? range = null;
        try
        {
            range = summarySheet.Range[summarySheet.Cells[1, lastColumn + 1], summarySheet.Cells[1, newLastColumn]];
            range.Value2 = headers;
        }
        finally
        {
            ComObject.Release(range);
        }

        return newLastColumn;
    }

    private static void FormatSummarySheet(OfficeExcel.Application excel, OfficeExcel.Worksheet summarySheet, int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
        {
            return;
        }

        OfficeExcel.Range? allDataRange = null;
        OfficeExcel.ListObject? table = null;
        try
        {
            allDataRange = summarySheet.Range[summarySheet.Cells[1, 1], summarySheet.Cells[rows, columns]];
            table = summarySheet.ListObjects.Add(
                OfficeExcel.XlListObjectSourceType.xlSrcRange,
                allDataRange,
                Type.Missing,
                OfficeExcel.XlYesNoGuess.xlYes);
            table.TableStyle = "TableStyleMedium2";
        }
        catch
        {
        }
        finally
        {
            ComObject.Release(table);
            ComObject.Release(allDataRange);
        }

        try
        {
            summarySheet.Activate();
            if (excel.Windows.Count > 0)
            {
                excel.ActiveWindow.SplitRow = 1;
                excel.ActiveWindow.FreezePanes = true;
            }
        }
        catch
        {
        }

        if (rows < 5000)
        {
            OfficeExcel.Range? columnsRange = null;
            try
            {
                columnsRange = summarySheet.Columns;
                columnsRange.AutoFit();
            }
            catch
            {
            }
            finally
            {
                ComObject.Release(columnsRange);
            }
        }
    }
}
