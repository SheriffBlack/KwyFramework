namespace Kwy.Device.Core.IO;

/// <summary>
/// 逻辑 IO 状态采集选项。
/// 此周期仅决定上位机缓存的更新频率，不构成实时响应或功能安全指标。
/// </summary>
public sealed class IoStateMonitorOptions
{
    /// <summary>轮询周期；支持硬件通知的设备仍会保留轮询作为状态补采集。</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(20);

    internal void Validate()
    {
        if (PollingInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(PollingInterval), "Polling interval must be greater than zero.");
    }
}
