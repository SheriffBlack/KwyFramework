using Kwy.Files.Excel.Abstractions;

namespace Kwy.Files.Excel.NPOI;

public sealed class NpoiExcelSheetMergeService : IExcelSheetMergeService
{
    private readonly IExcelWorkbookService workbookService;

    public NpoiExcelSheetMergeService(IExcelWorkbookService workbookService)
    {
        this.workbookService = workbookService;
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
        for (int i = 0; i < files.Length; i++)
        {
            var filePath = files[i];
            var result = new ExcelSheetMergeResult();
            try
            {
                await MergeSingleFileAsync(filePath, options, result, cancellationToken);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            results[filePath] = result;
            progress?.Report(new ExcelSheetMergeProgress(filePath, i + 1, files.Length, result));
        }

        return results;
    }

    private async Task MergeSingleFileAsync(
        string filePath,
        ExcelSheetMergeOptions options,
        ExcelSheetMergeResult result,
        CancellationToken cancellationToken)
    {
        await using var session = await workbookService.OpenAsync(new ExcelOpenOptions { FilePath = filePath, ReadOnly = false }, cancellationToken);
        var sheetNames = (await session.GetSheetNamesAsync(cancellationToken))
            .Where(name => !string.Equals(name, options.SummarySheetName, StringComparison.Ordinal))
            .ToArray();

        var output = new List<IReadOnlyList<object?>>();
        var headerWritten = false;
        foreach (var sheetName in sheetNames)
        {
            var sheet = await session.ReadSheetAsync(new ExcelReadOptions { SheetName = sheetName, UseFormattedText = false }, cancellationToken);
            result.ProcessedSheetCount++;
            if (!headerWritten)
            {
                output.Add(BuildHeader(sheet, options));
                headerWritten = true;
            }

            foreach (var row in sheet.Rows.Skip(Math.Max(0, options.StartRow - 1)))
            {
                if (ShouldSkip(row, options))
                {
                    continue;
                }

                var mergedRow = AddExtraColumns(row, options.AdditionalColumnsCount);
                output.Add(mergedRow);
                result.AllRowsData.Add(mergedRow.ToArray());
            }
        }

        await session.WriteRangeAsync(new ExcelWriteOptions
        {
            SheetName = options.SummarySheetName,
            StartRow = 1,
            StartColumn = 1,
            CreateSheetIfMissing = true,
            AutoFitColumns = true
        }, output, cancellationToken);
        await session.SaveAsync(cancellationToken);
        result.TotalRows = Math.Max(0, output.Count - 1);
    }

    private static IReadOnlyList<object?> BuildHeader(ExcelSheetData sheet, ExcelSheetMergeOptions options)
    {
        if (options.HeaderContent is { Length: > 0 })
        {
            return AddExtraColumns(options.HeaderContent.Cast<object?>().ToArray(), options.AdditionalColumnsCount, options.AdditionalColumnsHeaders);
        }

        var header = sheet.Rows.Skip(Math.Max(0, options.HeaderRow - 1)).FirstOrDefault() ?? Array.Empty<object?>();
        return AddExtraColumns(header, options.AdditionalColumnsCount, options.AdditionalColumnsHeaders);
    }

    private static IReadOnlyList<object?> AddExtraColumns(IReadOnlyList<object?> row, int count, string[]? headers = null)
    {
        if (count <= 0)
        {
            return row.ToArray();
        }

        var values = row.ToList();
        for (int i = 0; i < count; i++)
        {
            values.Add(headers != null && i < headers.Length ? headers[i] : null);
        }

        return values;
    }

    private static bool ShouldSkip(IReadOnlyList<object?> row, ExcelSheetMergeOptions options)
    {
        if (options.FilterCheckColumns is { Length: > 0 }
            && options.FilterCheckColumns.All(column => string.IsNullOrWhiteSpace(GetText(row, column))))
        {
            return true;
        }

        if (options.ExcludeRules == null)
        {
            return false;
        }

        foreach (var (column, excludedValues) in options.ExcludeRules)
        {
            var text = GetText(row, column);
            if (excludedValues.Any(value => string.Equals(value, text, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetText(IReadOnlyList<object?> row, int oneBasedColumn)
    {
        int index = oneBasedColumn - 1;
        return index >= 0 && index < row.Count ? row[index]?.ToString()?.Trim() ?? string.Empty : string.Empty;
    }
}
