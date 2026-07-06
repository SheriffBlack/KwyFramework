using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Communicate.Secs;

public sealed class SecsHsmsConfig : IProtocolConfig, IKeepAliveConfig
{
    public ProtocolType ProtocolType => ProtocolType.Secs;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5000;

    public HsmsConnectionMode Mode { get; set; } = HsmsConnectionMode.Active;

    public ushort DeviceId { get; set; }

    public int Timeout { get; set; } = 5000;

    public int T3Timeout { get; set; } = 45000;

    public int T5Timeout { get; set; } = 10000;

    public int T6Timeout { get; set; } = 5000;

    public int T7Timeout { get; set; } = 10000;

    public int T8Timeout { get; set; } = 5000;

    public bool AutoReconnect { get; set; } = true;

    public int MaxReconnectAttempts { get; set; } = 5;

    public int ReconnectInterval { get; set; } = 2000;

    public bool KeepAlive { get; set; } = true;

    public int KeepAliveInterval { get; set; } = 30000;

    public bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Host)
            && Port is > 0 and <= 65535
            && Timeout > 0
            && T3Timeout > 0
            && T5Timeout > 0
            && T6Timeout > 0
            && T7Timeout > 0
            && T8Timeout > 0
            && MaxReconnectAttempts >= 0
            && ReconnectInterval >= 0
            && (!KeepAlive || KeepAliveInterval > 0);
    }
}
