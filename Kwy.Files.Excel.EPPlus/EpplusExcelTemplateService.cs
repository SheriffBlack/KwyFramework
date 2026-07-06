using Kwy.Files.Excel.Abstractions;
using OfficeOpenXml;

namespace Kwy.Files.Excel.EPPlus;

public sealed class EpplusExcelTemplateService : IExcelTemplateService
{
    private readonly ExcelEpplusOptions options;

    public EpplusExcelTemplateService(ExcelEpplusOptions options)
    {
        this.options = options;
    }

    public Task CopySheetFromTemplateAsync(ExcelTemplateCopyOptions copyOptions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copyOptions);
        copyOptions.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        EnsureXlsx(copyOptions.TemplateFilePath);
        EnsureXlsx(copyOptions.TargetFilePath);
        if (!File.Exists(copyOptions.TemplateFilePath))
        {
            throw new FileNotFoundException($"Template file does not exist: {copyOptions.TemplateFilePath}", copyOptions.TemplateFilePath);
        }

        ExcelPackage.LicenseContext = options.LicenseContext;
        using var templatePackage = new ExcelPackage(new FileInfo(copyOptions.TemplateFilePath));
        var sourceSheet = templatePackage.Workbook.Worksheets[copyOptions.TemplateSheetName]
            ?? throw new ArgumentException($"Template worksheet does not exist: {copyOptions.TemplateSheetName}", nameof(copyOptions));

        var targetFile = new FileInfo(copyOptions.TargetFilePath);
        using var targetPackage = targetFile.Exists ? new ExcelPackage(targetFile) : new ExcelPackage();
        var targetSheetName = copyOptions.NewSheetName ?? copyOptions.TemplateSheetName;
        var existing = targetPackage.Workbook.Worksheets[targetSheetName];
        if (existing != null)
        {
            if (!copyOptions.ReplaceIfExists)
            {
                throw new InvalidOperationException($"Worksheet already exists: {targetSheetName}");
            }

            targetPackage.Workbook.Worksheets.Delete(existing);
        }

        targetPackage.Workbook.Worksheets.Add(targetSheetName, sourceSheet);
        targetPackage.SaveAs(targetFile);
        return Task.CompletedTask;
    }

    private static void EnsureXlsx(string filePath)
    {
        if (ExcelFileFormatHelper.DetectFromPath(filePath) != ExcelFileFormat.Xlsx)
        {
            throw new NotSupportedException("EPPlus template operations only support .xlsx workbooks.");
        }
    }
}
