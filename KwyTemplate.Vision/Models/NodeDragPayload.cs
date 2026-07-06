using System.Windows;

namespace KwyTemplate.Vision.Models;

public sealed class NodeDragPayload
{
    public NodeDragPayload(string nodeType, Vector anchorOffset)
    {
        NodeType = nodeType;
        AnchorOffset = anchorOffset;
    }

    public string NodeType { get; }

    /// <summary>
    /// 鼠标相对节点左上角的偏移。画布落点 = 鼠标逻辑坐标 - AnchorOffset。
    /// </summary>
    public Vector AnchorOffset { get; }
}
