using Kwy.Communicate.TcpSerial.Configs;
using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.MarkPrinters;

public sealed class MarkPrintConfig : IDeviceConfig
{
    public TcpConfig Tcp { get; set; } = CreateDefaultTcpConfig();

    public bool Validate()
        => Tcp is not null && Tcp.Validate();

    public static TcpConfig CreateDefaultTcpConfig()
        => new()
        {
            Host = "127.0.0.1",
            Port = 1680,
            Timeout = 3000,
            ReceiveTimeout = 3000,
            SendTimeout = 3000,
            KeepAlive = true,
            AutoReconnect = false,
            MaxReconnectAttempts = 0
        };
}
