using NPOI.SS.UserModel;

namespace Kwy.Files.Excel.NPOI;

internal static class NpoiExcelValueConverter
{
    public static object? GetValue(ICell? cell, bool formatted)
    {
        if (cell == null)
        {
            return null;
        }

        if (formatted)
        {
            return cell.ToString();
        }

        return cell.CellType switch
        {
            CellType.Blank => null,
            CellType.Boolean => cell.BooleanCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue
                : cell.NumericCellValue,
            CellType.String => cell.StringCellValue,
            CellType.Formula => GetFormulaValue(cell),
            CellType.Error => cell.ErrorCellValue,
            _ => cell.ToString()
        };
    }

    public static void SetValue(ICell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.SetBlank();
                break;
            case DateTime dateTime:
                cell.SetCellValue(dateTime);
                break;
            case bool boolean:
                cell.SetCellValue(boolean);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.SetCellValue(Convert.ToDouble(value));
                break;
            default:
                cell.SetCellValue(value.ToString() ?? string.Empty);
                break;
        }
    }

    private static object? GetFormulaValue(ICell cell)
    {
        return cell.CachedFormulaResultType switch
        {
            CellType.Boolean => cell.BooleanCellValue,
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue
                : cell.NumericCellValue,
            CellType.String => cell.StringCellValue,
            CellType.Error => cell.ErrorCellValue,
            _ => cell.ToString()
        };
    }
}
