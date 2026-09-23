namespace Kwy.Device.Core.IO;

/// <summary>
/// IO 通道与端口数量的统一校验工具。
/// 物理驱动、逻辑点位初始化和位运算辅助均使用此处定义的 64 点边界。
/// </summary>
public static class IoChannelGuard
{
    /// <summary>当前公共 IO 模型支持的最大通道数。</summary>
    public const int MaxChannelCount = 64;
    public const int MaxPortCount = MaxChannelCount / 8;

    /// <summary>校验物理通道索引位于设备实际通道范围内。</summary>
    public static void ValidateChannel(int channel, int channelCount, string parameterName)
    {
        ValidateChannelCount(channelCount, nameof(channelCount));

        if (channel < 0 || channel >= channelCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                channel,
                $"Channel must be between 0 and {channelCount - 1}.");
        }
    }

    /// <summary>校验设备声明的通道数量符合当前模型边界。</summary>
    public static void ValidateChannelCount(int channelCount, string parameterName)
    {
        if (channelCount is < 1 or > MaxChannelCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                channelCount,
                $"Channel count must be between 1 and {MaxChannelCount}.");
        }
    }

    /// <summary>校验端口数量不超过 64 点掩码可表达的八个字节。</summary>
    public static void ValidatePortCount(int portCount, string parameterName)
    {
        if (portCount is < 1 or > MaxPortCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                portCount,
                $"Port count must be between 1 and {MaxPortCount}.");
        }
    }
}
