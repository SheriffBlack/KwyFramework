using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.TcpSerial.Configs;
using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.Scanners;

public sealed class BarcodeScannerConfig : IDeviceConfig
{
    public SerialPortConfig Serial { get; set; } = CreateDefaultSerialConfig();

    public bool Validate()
        => Serial is not null && Serial.Validate();

    public static SerialPortConfig CreateDefaultSerialConfig()
        => new()
        {
            Port = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            Parity = ParityType.None,
            StopBits = StopBitsType.One,
            ReadTimeout = 500,
            WriteTimeout = 500,
            KeepAlive = false,
            AutoReconnect = false,
            MaxReconnectAttempts = 0
        };
}
