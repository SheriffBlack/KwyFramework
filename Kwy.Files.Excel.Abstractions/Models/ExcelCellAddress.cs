namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// 1-based Excel cell address.
/// </summary>
public readonly record struct ExcelCellAddress
{
    public ExcelCellAddress(int row, int column)
    {
        if (row < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be greater than or equal to 1.");
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be greater than or equal to 1.");
        }

        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }
}
