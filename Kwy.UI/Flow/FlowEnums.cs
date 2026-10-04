namespace Kwy.UI.Flow;

/// <summary>
/// 流程端口的数据流方向。
/// </summary>
public enum FlowPortDirection
{
    Input,
    Output
}

/// <summary>
/// 流程端口在节点上的停靠边。
/// </summary>
public enum FlowPortSide
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// 流程端口承载的数据类别。
/// </summary>
public enum FlowPortType
{
    Data,
    Execution
}

/// <summary>
/// 节点的通用视觉状态。
/// </summary>
public enum FlowNodeVisualState
{
    Idle,
    Running,
    Success,
    Failed,
    Paused
}
