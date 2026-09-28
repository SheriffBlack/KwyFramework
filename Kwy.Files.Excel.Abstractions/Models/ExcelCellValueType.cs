namespace Kwy.Files.Excel.Abstractions.Models;

/// <summary>
/// Logical cell value type.
/// </summary>
public enum ExcelCellValueType
{
    Empty,
    Text,
    Number,
    Boolean,
    DateTime,
    Formula,
    Error
}
