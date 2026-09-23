namespace Kwy.Device.Core.IO;

/// <summary>
/// 在厂商端口字节数组、逐点布尔数组与 64 位掩码之间进行转换。
/// 不包含反相、点位 ID 等业务语义；这些由逻辑 IO 层处理。
/// </summary>
public static class IoBitConverter
{
    /// <summary>公共 IO 模型当前支持的最大通道数。</summary>
    public const int DefaultChannelCount = 64;

    /// <summary>按低位到高位的顺序，将端口数据展开为物理通道状态。</summary>
    public static bool[] ToBits(byte[] portData, int length = DefaultChannelCount)
    {
        ArgumentNullException.ThrowIfNull(portData);
        IoChannelGuard.ValidateChannelCount(length, nameof(length));

        var bits = new bool[length];
        for (int port = 0; port < portData.Length; port++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                int index = port * 8 + bit;
                if (index >= bits.Length)
                {
                    return bits;
                }

                bits[index] = (portData[port] & (1 << bit)) != 0;
            }
        }

        return bits;
    }

    /// <summary>将最多八个端口字节转换为物理 IO 掩码。</summary>
    public static ulong ToMask(byte[] portData)
    {
        ArgumentNullException.ThrowIfNull(portData);

        int portCount = Math.Min(portData.Length, DefaultChannelCount / 8);
        ulong mask = 0;
        for (int port = 0; port < portCount; port++)
        {
            mask |= ((ulong)portData[port]) << (port * 8);
        }

        return mask;
    }

    /// <summary>将掩码拆分为指定数量的端口字节，供支持端口批量读写的驱动调用。</summary>
    public static byte[] ToPortBytes(ulong mask, int portCount)
    {
        IoChannelGuard.ValidatePortCount(portCount, nameof(portCount));

        var portData = new byte[portCount];
        for (int port = 0; port < portData.Length; port++)
        {
            portData[port] = (byte)((mask >> (port * 8)) & 0xFF);
        }

        return portData;
    }

    /// <summary>生成低 <paramref name="channelCount"/> 位为 1 的可写掩码。</summary>
    public static ulong CreateWritableMask(int channelCount)
    {
        IoChannelGuard.ValidateChannelCount(channelCount, nameof(channelCount));

        return channelCount >= DefaultChannelCount
            ? ulong.MaxValue
            : (1UL << channelCount) - 1;
    }
}
