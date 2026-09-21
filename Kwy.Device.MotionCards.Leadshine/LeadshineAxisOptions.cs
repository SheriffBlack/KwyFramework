namespace Kwy.Device.MotionCards.Leadshine;

/// <summary>雷赛控制器的轴级专属参数。</summary>
public sealed record LeadshineAxisOptions
{
    /// <summary>雷赛 dmc_set_homemode 专用的回零模式。</summary>
    public ushort HomeMode { get; init; } = 1;

    /// <summary>雷赛回零时使用的 EZ 信号计数。</summary>
    public ushort EzCount { get; init; }
}
