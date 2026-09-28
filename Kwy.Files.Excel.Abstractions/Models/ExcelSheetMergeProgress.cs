namespace Kwy.Files.Excel.Abstractions.Models;

public sealed record ExcelSheetMergeProgress(
    string FilePath,
    int FileIndex,
    int FileCount,
    ExcelSheetMergeResult? Result = null);
