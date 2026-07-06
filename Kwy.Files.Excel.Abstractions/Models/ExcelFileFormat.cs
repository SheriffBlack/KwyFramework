namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Excel file format.
/// </summary>
public enum ExcelFileFormat
{
    /// <summary>
    /// Detects the format from the file extension.
    /// </summary>
    Auto,

    /// <summary>
    /// Excel 97-2003 workbook (*.xls).
    /// </summary>
    Xls,

    /// <summary>
    /// Excel workbook (*.xlsx).
    /// </summary>
    Xlsx,

    /// <summary>
    /// Comma-separated values (*.csv).
    /// </summary>
    Csv
}
