namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Provider-neutral workbook data.
/// </summary>
public sealed class ExcelWorkbookData
{
    public string? SourcePath { get; set; }

    public ExcelFileFormat Format { get; set; } = ExcelFileFormat.Auto;

    public List<ExcelSheetData> Sheets { get; } = new();
}
