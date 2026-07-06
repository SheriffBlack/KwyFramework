namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// 1-based Excel rectangular range address.
/// </summary>
public readonly record struct ExcelRangeAddress
{
    public ExcelRangeAddress(int startRow, int startColumn, int rowCount, int columnCount)
    {
        if (startRow < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startRow), startRow, "Start row must be greater than or equal to 1.");
        }

        if (startColumn < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(startColumn), startColumn, "Start column must be greater than or equal to 1.");
        }

        if (rowCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "Row count cannot be negative.");
        }

        if (columnCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "Column count cannot be negative.");
        }

        StartRow = startRow;
        StartColumn = startColumn;
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public int StartRow { get; }

    public int StartColumn { get; }

    public int RowCount { get; }

    public int ColumnCount { get; }
}
