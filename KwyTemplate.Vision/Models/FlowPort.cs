namespace KwyTemplate.Vision.Models;

/// <summary>
/// 节点端口：描述一个输入或输出插槽
/// </summary>
public class FlowPort
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>端口显示名，如 "图像输入"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>端口方向</summary>
    public PortDirection Direction { get; set; }

    /// <summary>数据类型标识，见 PortDataTypes。用于连线合法性检查。</summary>
    public string DataType { get; set; } = PortDataTypes.Any;

    /// <summary>是否允许多路连入（仅对 Input 端口有效）</summary>
    public bool AllowMultiple { get; set; } = false;

    /// <summary>端口位置 (Left, Top, Right, Bottom)</summary>
    public PortSide Side { get; set; } = PortSide.Left;

    /// <summary>端口类型 (Data, Execution)</summary>
    public PortType Type { get; set; } = PortType.Data;
}
