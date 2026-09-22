namespace Kwy.UI.DataGrids;

/// <summary>
/// 描述动态单元格的呈现级验证反馈。
/// 消费应用程序必须将业务结果映射到此状态。
/// </summary>
public enum CellValidationState
{
    None,
    Success,
    Warning,
    Error
}
