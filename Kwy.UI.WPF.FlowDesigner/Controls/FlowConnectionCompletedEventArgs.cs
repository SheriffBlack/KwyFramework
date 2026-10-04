namespace Kwy.UI.WPF.FlowDesigner.Controls;

/// <summary>
/// 流程编辑器完成端口连接时的通用命令参数。
/// </summary>
public sealed class FlowConnectionCompletedEventArgs
{
    public FlowConnectionCompletedEventArgs(
        object? sourceConnector,
        object? sourceNode,
        object? targetConnector,
        object? targetNode)
    {
        SourceConnector = sourceConnector;
        SourceNode = sourceNode;
        TargetConnector = targetConnector;
        TargetNode = targetNode;
    }

    public object? SourceConnector { get; }

    public object? SourceNode { get; }

    public object? TargetConnector { get; }

    public object? TargetNode { get; }
}
