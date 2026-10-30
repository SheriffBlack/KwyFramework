namespace Kwy.Device.Motion.Abstractions;

/// <summary>运动状态的底层采集方式。</summary>
public enum MotionStateAcquisitionMode
{
    /// <summary>使用线程池定时器轮询同步 SDK。</summary>
    Polling,

    /// <summary>使用专用线程轮询同步 SDK。</summary>
    DedicatedThread,

    /// <summary>由厂商回调或事件推送。</summary>
    Callback,

    /// <summary>由 EtherCAT PDO 过程映像提供。</summary>
    EtherCatPdo,

    /// <summary>组合快速状态与低频诊断数据。</summary>
    Hybrid
}
