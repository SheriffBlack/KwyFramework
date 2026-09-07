namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelReadOptions
{
    public string? SheetName { get; set; }

    public int StartRow { get; set; } = 1;

    public int StartColumn { get; set; } = 1;

    public int? RowCount { get; set; }

    public int? ColumnCount { get; set; }

    public bool UseFormattedText { get; set; } = true;

    public void Validate()
    {
        if (StartRow < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(StartRow), StartRow, "Start row must be greater than or equal to 1.");
        }

        if (StartColumn < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(StartColumn), StartColumn, "Start column must be greater than or equal to 1.");
        }

        if (RowCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RowCount), RowCount, "Row count cannot be negative.");
        }

        if (ColumnCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ColumnCount), ColumnCount, "Column count cannot be negative.");
        }
    }
}
