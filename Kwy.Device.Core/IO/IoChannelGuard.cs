namespace Kwy.Device.Core.IO;

/// <summary>
/// Common IO channel validation helpers.
/// </summary>
public static class IoChannelGuard
{
    public const int MaxChannelCount = 64;
    public const int MaxPortCount = MaxChannelCount / 8;

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
