using Kwy.Files.Excel.Abstractions;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

internal static class ExcelInteropConverters
{
    public static ExcelFileFormat NormalizeFormat(string? filePath, ExcelFileFormat format)
    {
        return format == ExcelFileFormat.Auto && !string.IsNullOrWhiteSpace(filePath)
            ? ExcelFileFormatHelper.DetectFromPath(filePath)
            : format;
    }

    public static OfficeExcel.XlFileFormat ToExcelFileFormat(ExcelFileFormat format)
    {
        return format switch
        {
            ExcelFileFormat.Xls => OfficeExcel.XlFileFormat.xlWorkbookNormal,
            ExcelFileFormat.Xlsx => OfficeExcel.XlFileFormat.xlOpenXMLWorkbook,
            ExcelFileFormat.Csv => OfficeExcel.XlFileFormat.xlCSV,
            _ => OfficeExcel.XlFileFormat.xlOpenXMLWorkbook
        };
    }

    public static object ToMissingIfNull(string? value)
    {
        return string.IsNullOrEmpty(value) ? Type.Missing : value;
    }
}
