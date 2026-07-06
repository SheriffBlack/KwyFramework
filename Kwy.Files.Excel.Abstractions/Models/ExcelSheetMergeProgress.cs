namespace Kwy.Files.Excel.Abstractions;

public sealed record ExcelSheetMergeProgress(
    string FilePath,
    int FileIndex,
    int FileCount,
    ExcelSheetMergeResult? Result = null);
