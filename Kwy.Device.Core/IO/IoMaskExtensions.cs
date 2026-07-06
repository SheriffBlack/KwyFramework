namespace Kwy.Device.Core.IO;

/// <summary>
/// Io卡 状态掩码的扩展方法
/// </summary>
public static class IoMaskExtensions
{
    /// <summary>
    /// 判断快照中的某个特定引脚是否为高电平 (触发状态)
    /// </summary>
    /// <param name="mask">64位的快照数据</param>
    /// <param name="pinIndex">引脚号 (例如 IN00 就是 0，IN10 就是 10)</param>
    /// <returns></returns>
    public static bool IsPinActive(this ulong mask, int pinIndex)
    {
        if (pinIndex < 0 || pinIndex >= IoChannelGuard.MaxChannelCount)
        {
            return false;
        }

        // 核心位运算：将 1UL 左移 pinIndex 位，然后与 mask 进行按位与 (64位安全)
        return (mask & (1UL << pinIndex)) != 0;
    }

    public static ulong SetPin(this ulong mask, int pinIndex, bool active)
    {
        IoChannelGuard.ValidateChannel(pinIndex, IoChannelGuard.MaxChannelCount, nameof(pinIndex));
        return active ? mask | (1UL << pinIndex) : mask & ~(1UL << pinIndex);
    }
}
