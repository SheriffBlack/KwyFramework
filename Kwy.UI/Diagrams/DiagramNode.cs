namespace Kwy.UI.Diagrams;

/// <summary>
/// 通用二维图中的节点描述。
/// X 和 Y 表示节点左上角的逻辑坐标。
/// </summary>
public sealed record DiagramNode(
    string Id,
    double X,
    double Y,
    double Width,
    double Height,
    string? Label = null,
    DiagramNodeState State = DiagramNodeState.Normal);
