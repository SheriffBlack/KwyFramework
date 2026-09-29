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
    /// <summary>空闲。</summary>
    Idle,
    /// <summary>已就绪，可以开始运行。</summary>
    Ready,
    /// <summary>正在运行。</summary>
    Running,
    /// <summary>已暂停。</summary>
    Paused,
    /// <summary>正在停止。</summary>
    Stopping,
    /// <summary>处于维护状态。</summary>
    Maintenance,
    /// <summary>发生故障。</summary>
    Faulted
}
