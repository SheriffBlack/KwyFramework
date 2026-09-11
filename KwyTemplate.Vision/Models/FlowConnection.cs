namespace KwyTemplate.Vision.Models;

/// <summary>
/// 连线：描述两个端口之间的数据流向
/// </summary>
public class FlowConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>源端口（Output）的 Id</summary>
    public Guid SourcePortId { get; set; }

    /// <summary>目标端口（Input）的 Id</summary>
    public Guid TargetPortId { get; set; }

    /// <summary>是否设置了断点 (LabVIEW 风格执行调试)</summary>
    public bool HasBreakpoint { get; set; }

    /// <summary>是否添加了数据探针</summary>
    public bool HasProbe { get; set; }
}
