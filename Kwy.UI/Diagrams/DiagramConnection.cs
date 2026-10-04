namespace Kwy.UI.Diagrams;

/// <summary>
/// 通用二维图中两个节点之间的连接关系。
/// </summary>
public sealed record DiagramConnection(
    string SourceId,
    string TargetId,
    string? Label = null,
    DiagramConnectionState State = DiagramConnectionState.Normal,
    double Thickness = 1d,
    double? TargetThickness = null,
    DiagramConnectionShape Shape = DiagramConnectionShape.Line);
