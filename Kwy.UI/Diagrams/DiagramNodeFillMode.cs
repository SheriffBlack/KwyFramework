namespace Kwy.UI.Diagrams;

/// <summary>
/// 图节点填充的主题表达方式。
/// </summary>
public enum DiagramNodeFillMode
{
    /// <summary>
    /// 使用控件默认背景色。
    /// </summary>
    Default,

    /// <summary>
    /// 使用状态对应的强调色填充。
    /// </summary>
    State,

    /// <summary>
    /// 使用状态对应的浅色填充，并保留状态边框。
    /// </summary>
    SoftState
}
