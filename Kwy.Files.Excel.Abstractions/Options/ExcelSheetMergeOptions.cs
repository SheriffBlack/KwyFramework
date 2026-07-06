namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelSheetMergeOptions
{
    public string SummarySheetName { get; set; } = "全表汇总";

    public int StartRow { get; set; } = 10;

    public int HeaderRow { get; set; } = 1;

    public string[]? HeaderContent { get; set; }

    public int AdditionalColumnsCount { get; set; }

    public string[]? AdditionalColumnsHeaders { get; set; }

    public int[]? FilterCheckColumns { get; set; }

    public Dictionary<int, string[]>? ExcludeRules { get; set; }

    public bool ShowApplicationWindow { get; set; }

    public bool WaitForUserInput { get; set; }
}
