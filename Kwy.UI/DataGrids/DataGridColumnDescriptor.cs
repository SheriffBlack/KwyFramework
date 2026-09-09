namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格列描述默认实现。
/// </summary>
public class DataGridColumnDescriptor : IDataGridColumnDescriptor
{
    public string Key { get; set; } = string.Empty;

    public object? Header { get; set; }

    public string BindingPath { get; set; } = string.Empty;
}
