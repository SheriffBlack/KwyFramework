namespace KwyPecvd.Process.Chamber.Vacuum;

/// <summary>
/// 真空子系统状态
/// </summary>
public enum ChamberVacuumState
{
    /// <summary>尚未完成硬件检查。</summary>
    Offline,

    /// <summary>硬件正常，尚未开始抽气。</summary>
    Idle,

    /// <summary>已经下发启动泵命令，等待泵运行反馈。</summary>
    StartingPump,

    /// <summary>干泵已运行，正在等待隔离阀打开反馈。</summary>
    OpeningValves,

    /// <summary>隔离阀已经打开，腔体正在降压。</summary>
    Pumping,

    /// <summary>压力已经达到工艺准备条件。</summary>
    Ready,

    /// <summary>正在关闭隔离阀并停止真空系统。</summary>
    Stopping,

    /// <summary>真空系统发生故障。</summary>
    Faulted
}

/*
 
 Offline
   │ 硬件反馈有效
   ▼
 Idle
   │ 接受启动泵命令
   ▼
StartingPump
   │ 所有泵Actual.IsRunning
   │ 然后下发开阀命令
   ▼
OpeningValves
   │ 所有阀Actual.State == Open
   ▼
Pumping
   │ 压力Actual <= ReadyPressure
   ▼
 Ready
 
 
 */