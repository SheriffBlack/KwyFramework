namespace KwyPecvd.Device.ThrottleValve;

/// <summary>
/// 节流阀控制模式。
/// </summary>
public enum ThrottleValveMode
{
    Unknown = 0,

    /// <summary>强制关闭。</summary>
    Closed = 1,

    /// <summary>强制全开。</summary>
    Open = 2,

    /// <summary>按阀门开度控制。</summary>
    Position = 3,

    /// <summary>按腔体压力闭环控制。</summary>
    Pressure = 4
}