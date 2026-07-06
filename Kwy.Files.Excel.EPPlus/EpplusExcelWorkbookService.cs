using Kwy.Files.Excel.Abstractions;
using OfficeOpenXml;

namespace Kwy.Files.Excel.EPPlus;

public sealed class EpplusExcelWorkbookService : IExcelWorkbookService
{
    private readonly ExcelEpplusOptions options;

    public EpplusExcelWorkbookService(ExcelEpplusOptions options)
    {
        this.options = options;
    }

    public ExcelProviderInfo ProviderInfo { get; } = new(
        "EPPlus",
        ExcelProviderFeatures.ReadWorkbook
        | ExcelProviderFeatures.WriteWorkbook
        | ExcelProviderFeatures.Xlsx
        | ExcelProviderFeatures.TemplateCopy
        | ExcelProviderFeatures.PreserveStyles
        | ExcelProviderFeatures.PreservePictures
        | ExcelProviderFeatures.PreserveMergedCells
        | ExcelProviderFeatures.Formula,
        new HashSet<ExcelFileFormat> { ExcelFileFormat.Xlsx });

    public Task<IExcelWorkbookSession> OpenAsync(ExcelOpenOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(options.FilePath))
        {
            throw new FileNotFoundException($"Excel file does not exist: {options.FilePath}", options.FilePath);
        }

        EnsureSupportedFormat(options.FilePath, options.Format);
        ConfigureLicense();
        var package = new ExcelPackage(new FileInfo(options.FilePath), options.Password);
        return Task.FromResult<IExcelWorkbookSession>(new EpplusExcelWorkbookSession(
            package,
            options.FilePath,
            ExcelFileFormat.Xlsx,
            options.ReadOnly));
    }

    public Task<IExcelWorkbookSession> CreateAsync(ExcelFileFormat format = ExcelFileFormat.Xlsx, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (format is not ExcelFileFormat.Auto and not ExcelFileFormat.Xlsx)
        {
            throw new NotSupportedException("EPPlus provider only supports .xlsx workbooks.");
        }

        ConfigureLicense();
        var package = new ExcelPackage();
        package.Workbook.Worksheets.Add("Sheet1");
        return Task.FromResult<IExcelWorkbookSession>(new EpplusExcelWorkbookSession(package, null, ExcelFileFormat.Xlsx, readOnly: false));
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

    private void ConfigureLicense()
    {
        ExcelPackage.LicenseContext = options.LicenseContext;
    }

    private static void EnsureSupportedFormat(string filePath, ExcelFileFormat format)
    {
        var detected = format == ExcelFileFormat.Auto ? ExcelFileFormatHelper.DetectFromPath(filePath) : format;
        if (detected != ExcelFileFormat.Xlsx)
        {
            throw new NotSupportedException("EPPlus provider only supports .xlsx workbooks.");
        }
    }
}
