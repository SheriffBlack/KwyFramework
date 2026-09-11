namespace KwyTemplate.Vision.Models;

/// <summary>
/// 流程节点：画布上的一个功能块
/// </summary>
public class FlowNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>节点类型唯一标识，与 IFlowNodeDescriptor.NodeType 对应</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>显示名称（可由用户改名）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>节点在画布上的 X 坐标</summary>
    public double X { get; set; }

    /// <summary>节点在画布上的 Y 坐标</summary>
    public double Y { get; set; }

    /// <summary>输入端口列表</summary>
    public List<FlowPort> InputPorts { get; set; } = new();

    /// <summary>输出端口列表</summary>
    public List<FlowPort> OutputPorts { get; set; } = new();

    /// <summary>
    /// 节点参数键值对（供属性面板编辑）
    /// Key = 参数名, Value = 参数值（JSON 序列化兼容类型）
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>节点是否被禁用 (LabVIEW 风格：忽略该节点执行)</summary>
    public bool IsDisabled { get; set; }

    /// <summary>用户备注/描述（用于记录设计思路）</summary>
    public string Comment { get; set; } = string.Empty;
}
