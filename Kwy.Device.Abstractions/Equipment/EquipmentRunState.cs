namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 表示独立于具体通信协议的整机运行状态。
/// </summary>
public enum EquipmentRunState
{
    /// <summary>状态未知。</summary>
    Unknown,
    /// <summary>正在初始化。</summary>
    Initializing,
    /// <summary>空闲。整机未运行且已停止，但尚未确认满足自动运行前置条件。</summary>
    Idle,
    /// <summary>已就绪，可以开始运行。已完成自动运行前检查，所有必要条件均满足，等待启动</summary>
    Ready,
    /// <summary>正在运行。</summary>
    Running,
    /// <summary>已暂停。</summary>
    Paused,
    /// <summary>正在停止。</summary>
    Stopping,
    /// <summary>发生故障。</summary>
    Faulted
}


public enum EquipmentOperationMode
{
    /// <summary>自动模式：由EAP/上位机自动下发Recipe、自动时序，Fab产线自动化</summary>
    Automatic,
    /// <summary>手动模式：本地UI人工单步操作阀门、轴、IO，不跑完整Recipe自动流程</summary>
    Manual,
    /// <summary>维护模式：工程师，部分安全联锁可旁路，用于维修、校准</summary>
    Maintenance,
    /// <summary>Setup：配方调试、试片、工艺参数调试、Recipe开发阶段</summary>
    Setup
}
