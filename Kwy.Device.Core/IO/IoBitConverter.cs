namespace Kwy.Device.Core.IO;

/// <summary>
/// Converts IO port bytes and 64-bit masks.
/// </summary>
public static class IoBitConverter
{
    public const int DefaultChannelCount = 64;

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

    public static ulong CreateWritableMask(int channelCount)
    {
        IoChannelGuard.ValidateChannelCount(channelCount, nameof(channelCount));

        return channelCount >= DefaultChannelCount
            ? ulong.MaxValue
            : (1UL << channelCount) - 1;
    }
}
