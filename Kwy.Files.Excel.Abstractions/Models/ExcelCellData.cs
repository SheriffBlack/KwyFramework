namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Provider-neutral Excel cell value.
/// </summary>
public sealed record ExcelCellData(
    object? Value,
    string? Text = null,
    string? Formula = null,
    ExcelCellValueType ValueType = ExcelCellValueType.Empty);
