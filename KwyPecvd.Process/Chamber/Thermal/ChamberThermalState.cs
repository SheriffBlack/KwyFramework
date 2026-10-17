namespace KwyPecvd.Process.Chamber.Thermal;

/// <summary>
/// 腔体热子系统的运行状态。
/// </summary>
public enum ChamberThermalState
{
    /// <summary>
    /// 温控硬件尚未就绪，或者存在离线设备。
    /// </summary>
    Offline,

    /// <summary>
    /// 热系统已就绪，但当前没有执行温度准备。
    /// </summary>
    Idle,

    /// <summary>
    /// 已下发温度目标，正在等待各温区进入允许范围。
    /// </summary>
    Heating,

    /// <summary>
    /// 所有温区已经进入允许范围，正在等待稳定时间。
    /// </summary>
    Stabilizing,

    /// <summary>
    /// 所有温区均已稳定达到目标温度。
    /// </summary>
    Ready,

    /// <summary>
    /// 正在停止并关闭温控输出。
    /// </summary>
    Stopping,

    /// <summary>
    /// 热系统发生故障。
    /// </summary>
    Faulted
}