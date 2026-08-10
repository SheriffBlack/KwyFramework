using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格列描述，不依赖具体 UI 技术栈。
/// </summary>
public interface IDataGridColumnDescriptor
{
    string ParameterId { get; }

    string DisplayName { get; }
}
