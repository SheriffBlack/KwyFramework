namespace Kwy.UI.DataGrids;

/// <summary>
/// Column descriptor for a value stored in a <see cref="DisplayRowItem"/> cell.
/// </summary>
public sealed class DynamicCellColumnDescriptor : DataGridColumnDescriptor
{
    public DynamicCellColumnDescriptor(string key, object? header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Header = header;
        BindingPath = $"Item[{key}].Value";
    }
}
