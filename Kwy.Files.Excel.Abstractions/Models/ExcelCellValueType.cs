namespace Kwy.Files.Excel.Abstractions;

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
