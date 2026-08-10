namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格列描述默认实现。
/// </summary>
public class DataGridColumnDescriptor : IDataGridColumnDescriptor
{
    public string ParameterId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
