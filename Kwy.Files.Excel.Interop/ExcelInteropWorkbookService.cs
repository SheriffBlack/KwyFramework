using Kwy.Files.Excel.Abstractions;
using Kwy.Files.Excel.Interop.Interop;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropWorkbookService : IExcelWorkbookService, IAsyncDisposable, IDisposable
{
    private readonly ExcelInteropApplication application;

    public ExcelInteropWorkbookService(ExcelInteropApplication application)
    {
        this.application = application;
    }

    public ExcelProviderInfo ProviderInfo { get; } = new(
        "Microsoft Office Interop Excel",
        ExcelProviderFeatures.ReadWorkbook
        | ExcelProviderFeatures.WriteWorkbook
        | ExcelProviderFeatures.Xls
        | ExcelProviderFeatures.Xlsx
        | ExcelProviderFeatures.Csv
        | ExcelProviderFeatures.Formula
        | ExcelProviderFeatures.InteropAutomation
        | ExcelProviderFeatures.EncryptedTransparentRead,
        new HashSet<ExcelFileFormat> { ExcelFileFormat.Xls, ExcelFileFormat.Xlsx, ExcelFileFormat.Csv });

    public async Task<IExcelWorkbookSession> OpenAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!File.Exists(options.FilePath))
        {
            throw new FileNotFoundException($"Excel file does not exist: {options.FilePath}", options.FilePath);
        }

        var format = ExcelInteropConverters.NormalizeFormat(options.FilePath, options.Format);
        var workbook = await application.RunAsync(excel =>
        {
            OfficeExcel.Workbooks? workbooks = null;
            try
            {
                workbooks = excel.Workbooks;
                return workbooks.Open(
                    options.FilePath,
                    UpdateLinks: 0,
                    ReadOnly: options.ReadOnly,
                    Password: ExcelInteropConverters.ToMissingIfNull(options.Password),
                    IgnoreReadOnlyRecommended: true);
            }
            finally
            {
                ComObject.Release(workbooks);
            }
        }, cancellationToken);

        return new ExcelInteropWorkbookSession(application, workbook, options.FilePath, format, options.ReadOnly);
    }

    public async Task<IExcelWorkbookSession> CreateAsync(ExcelFileFormat format = ExcelFileFormat.Xlsx, CancellationToken cancellationToken = default)
    {
        var normalizedFormat = format == ExcelFileFormat.Auto ? ExcelFileFormat.Xlsx : format;
        var workbook = await application.RunAsync(excel =>
        {
            OfficeExcel.Workbooks? workbooks = null;
            try
            {
                workbooks = excel.Workbooks;
                return workbooks.Add();
            }
            finally
            {
                ComObject.Release(workbooks);
            }
        }, cancellationToken);
        return new ExcelInteropWorkbookSession(application, workbook, null, normalizedFormat, readOnly: false);
    }

    public async Task<ExcelWorkbookData> ReadWorkbookAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        await using var session = await OpenAsync(options, cancellationToken);
        var workbookData = new ExcelWorkbookData
        {
            SourcePath = session.FilePath,
            Format = session.Format
        };

        foreach (var sheetName in await session.GetSheetNamesAsync(cancellationToken))
        {
            workbookData.Sheets.Add(await session.ReadSheetAsync(new ExcelReadOptions { SheetName = sheetName }, cancellationToken));
        }

        return workbookData;
    }

    public async Task<IReadOnlyList<string>> GetSheetNamesAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        await using var session = await OpenAsync(options, cancellationToken);
        return await session.GetSheetNamesAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        application.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        application.Dispose();
    }
}
