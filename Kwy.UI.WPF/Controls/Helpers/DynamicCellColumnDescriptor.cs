using Kwy.UI.DataGrids;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// WPF column descriptor for a value stored in a <see cref="DisplayRowItem"/> indexer cell.
/// </summary>
public sealed class DynamicCellColumnDescriptor : DataGridColumnDescriptor
{
    public DynamicCellColumnDescriptor(string key, object? header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Header = header;
        BindingPath = $"[{key}].Value";
    }
}
