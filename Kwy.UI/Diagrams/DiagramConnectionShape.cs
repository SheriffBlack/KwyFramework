namespace Kwy.UI.Diagrams;

/// <summary>
/// 图连接的几何外形。
/// </summary>
public enum DiagramConnectionShape
{
    /// <summary>
    /// 等宽直线。
    /// </summary>
    Line,

    /// <summary>
    /// 从源节点向目标节点逐渐收窄或扩宽的四边形连接臂。
    /// </summary>
    Tapered
}
