namespace Kwy.Files.Excel.Abstractions.Models;

public sealed class ExcelSheetMergeResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public int TotalRows { get; set; }

    public int ProcessedSheetCount { get; set; }

    public List<object?[]> AllRowsData { get; } = new();
}
