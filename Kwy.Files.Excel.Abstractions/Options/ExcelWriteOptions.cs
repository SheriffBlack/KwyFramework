namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelWriteOptions
{
    public string SheetName { get; set; } = "Sheet1";

    public int StartRow { get; set; } = 1;

    public int StartColumn { get; set; } = 1;

    public bool CreateSheetIfMissing { get; set; } = true;

    public bool AutoFitColumns { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SheetName))
        {
            throw new ArgumentException("Sheet name cannot be empty.", nameof(SheetName));
        }

        if (StartRow < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(StartRow), StartRow, "Start row must be greater than or equal to 1.");
        }

        if (StartColumn < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(StartColumn), StartColumn, "Start column must be greater than or equal to 1.");
        }
    }
}
