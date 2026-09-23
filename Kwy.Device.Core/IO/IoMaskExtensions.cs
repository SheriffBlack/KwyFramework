namespace Kwy.Device.Core.IO;

/// <summary>
/// 物理 IO 状态掩码的位操作扩展方法。
/// </summary>
public static class IoMaskExtensions
{
    /// <summary>
    /// 判断物理快照中的指定通道是否为高电平。
    /// </summary>
    /// <param name="mask">64 位物理输入或输出快照。</param>
    /// <param name="pinIndex">从零开始的物理通道号。</param>
    /// <returns>通道为高电平时返回 <see langword="true"/>。</returns>
    public static bool IsPinActive(this ulong mask, int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= IoChannelGuard.MaxChannelCount)
        {
            return false;
        }

        // 将目标通道移至对应位后与快照按位与，避免 int 位宽带来的截断。
        return (mask & (1UL << pinIndex)) != 0;
    }

    /// <summary>返回设置指定物理通道后的新掩码，不修改原掩码。</summary>
    public static ulong SetPin(this ulong mask, int pinIndex, bool active)
    {
        IoChannelGuard.ValidateChannel(pinIndex, IoChannelGuard.MaxChannelCount, nameof(pinIndex));
        return active ? mask | (1UL << pinIndex) : mask & ~(1UL << pinIndex);
    }
}
