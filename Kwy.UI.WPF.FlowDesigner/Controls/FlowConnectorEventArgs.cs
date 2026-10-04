namespace Kwy.UI.WPF.FlowDesigner.Controls;

/// <summary>
/// 流程编辑器端口交互的通用命令参数。
/// </summary>
public sealed class FlowConnectorEventArgs
{
    public FlowConnectorEventArgs(object? connector, object? ownerNode)
    {
        Connector = connector;
        OwnerNode = ownerNode;
    }

    /// <summary>
    /// 获取端口绑定的数据项。
    /// </summary>
    public object? Connector { get; }

    /// <summary>
    /// 获取端口所属的节点数据项。
    /// </summary>
    public object? OwnerNode { get; }
}
