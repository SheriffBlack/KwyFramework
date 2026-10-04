namespace Kwy.UI.Diagrams;

/// <summary>
/// 通用图节点的视觉状态。
/// 具体平台可将状态映射为主题色、线型或动画。
/// </summary>
public enum DiagramNodeState
{
    Normal,
    Active,
    Disabled,
    Warning,
    Error
}
