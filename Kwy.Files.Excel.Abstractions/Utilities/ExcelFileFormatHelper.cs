namespace Kwy.Files.Excel.Abstractions;

public static class ExcelFileFormatHelper
{
    public static ExcelFileFormat DetectFromPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".xls" => ExcelFileFormat.Xls,
            ".xlsx" => ExcelFileFormat.Xlsx,
            ".csv" => ExcelFileFormat.Csv,
            _ => ExcelFileFormat.Auto
        };
    }

    public static string GetDefaultExtension(ExcelFileFormat format)
    {
        return format switch
        {
            ExcelFileFormat.Xls => ".xls",
            ExcelFileFormat.Xlsx => ".xlsx",
            ExcelFileFormat.Csv => ".csv",
            _ => string.Empty
        };
    }
}
