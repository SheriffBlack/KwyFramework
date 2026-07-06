using Kwy.Files.Excel.Abstractions;
using NPOI.SS.UserModel;

namespace Kwy.Files.Excel.NPOI;

public sealed class NpoiExcelWorkbookService : IExcelWorkbookService
{
    public ExcelProviderInfo ProviderInfo { get; } = new(
        "NPOI",
        ExcelProviderFeatures.ReadWorkbook
        | ExcelProviderFeatures.WriteWorkbook
        | ExcelProviderFeatures.Xls
        | ExcelProviderFeatures.Xlsx
        | ExcelProviderFeatures.Formula,
        new HashSet<ExcelFileFormat> { ExcelFileFormat.Xls, ExcelFileFormat.Xlsx });

    public async Task<IExcelWorkbookSession> OpenAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!File.Exists(options.FilePath))
        {
            throw new FileNotFoundException($"Excel file does not exist: {options.FilePath}", options.FilePath);
        }

        await using var stream = new FileStream(options.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        var workbook = WorkbookFactory.Create(stream);
        var format = ExcelFileFormatHelper.DetectFromPath(options.FilePath);
        return new NpoiExcelWorkbookSession(workbook, options.FilePath, format, options.ReadOnly);
    }

    public Task<IExcelWorkbookSession> CreateAsync(ExcelFileFormat format = ExcelFileFormat.Xlsx, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = format is ExcelFileFormat.Auto or ExcelFileFormat.Csv ? ExcelFileFormat.Xlsx : format;
        return Task.FromResult<IExcelWorkbookSession>(new NpoiExcelWorkbookSession(
            NpoiExcelWorkbookSession.CreateWorkbook(normalized),
            null,
            normalized,
            readOnly: false));
    }

    public async Task<ExcelWorkbookData> ReadWorkbookAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        await using var session = await OpenAsync(options, cancellationToken);
        var workbook = new ExcelWorkbookData
        {
            SourcePath = session.FilePath,
            Format = session.Format
        };

        foreach (var sheetName in await session.GetSheetNamesAsync(cancellationToken))
        {
            workbook.Sheets.Add(await session.ReadSheetAsync(new ExcelReadOptions { SheetName = sheetName }, cancellationToken));
        }

        return workbook;
    }

    public async Task<IReadOnlyList<string>> GetSheetNamesAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        await using var session = await OpenAsync(options, cancellationToken);
        return await session.GetSheetNamesAsync(cancellationToken);
    }
}
