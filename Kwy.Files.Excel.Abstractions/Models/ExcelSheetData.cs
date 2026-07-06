namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Provider-neutral worksheet data.
/// </summary>
public sealed class ExcelSheetData
{
    public ExcelSheetData()
    {
    }

    public ExcelSheetData(string sheetName, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        SheetName = sheetName;
        Rows = rows;
    }

    /// <summary>
    /// Gets or sets the worksheet name.
    /// </summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the worksheet rows. The first dimension is row, the second dimension is column.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; set; } = Array.Empty<IReadOnlyList<object?>>();

    public static ExcelSheetData FromStrings(string sheetName, string[][] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new ExcelSheetData(sheetName, data.Select(row => (IReadOnlyList<object?>)row.Cast<object?>().ToArray()).ToArray());
    }

    public string[][] ToStringMatrix()
    {
        return Rows
            .Select(row => row.Select(value => value?.ToString() ?? string.Empty).ToArray())
            .ToArray();
    }
}
