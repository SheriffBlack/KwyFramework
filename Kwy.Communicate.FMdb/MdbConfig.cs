using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.FMdb.Enums;

namespace Kwy.Communicate.FMdb;

/// <summary>
/// Configuration for a FluentModbus TCP or RTU client.
/// </summary>
public sealed class MdbConfig : IProtocolConfig
{
    /// <inheritdoc />
    public ProtocolType ProtocolType => ProtocolType.Modbus;

    /// <summary>
    /// Gets or sets the Modbus transport.
    /// </summary>
    public MdbTransport Transport { get; set; } = MdbTransport.Tcp;

    /// <summary>
    /// Gets or sets the Modbus slave/unit identifier.
    /// </summary>
    public byte UnitIdentifier { get; set; } = 1;

    /// <summary>
    /// Gets or sets the byte order used for typed register conversion.
    /// </summary>
    public MdbByteOrder ByteOrder { get; set; } = MdbByteOrder.BigEndian;

    /// <summary>
    /// Gets or sets the TCP host name or IP address.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the TCP port.
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// Gets or sets the serial port name used by RTU transport.
    /// </summary>
    public string SerialPort { get; set; } = "COM1";

    /// <summary>
    /// Gets or sets the serial baud rate used by RTU transport.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Gets or sets the serial parity used by RTU transport.
    /// </summary>
    public ParityType Parity { get; set; } = ParityType.Even;

    /// <summary>
    /// Gets or sets the serial stop bits used by RTU transport.
    /// </summary>
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    /// <summary>
    /// Gets or sets the serial handshake mode used by RTU transport.
    /// </summary>
    public HandshakeType Handshake { get; set; } = HandshakeType.None;

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 3000;

    /// <summary>
    /// Gets or sets the read timeout in milliseconds.
    /// </summary>
    public int ReadTimeout { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the write timeout in milliseconds.
    /// </summary>
    public int WriteTimeout { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether automatic reconnection is enabled.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of reconnection attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the interval between reconnection attempts in milliseconds.
    /// </summary>
    public int ReconnectInterval { get; set; } = 1000;

    /// <inheritdoc />
    public bool Validate()
    {
        if (Timeout <= 0 || ReadTimeout <= 0 || WriteTimeout <= 0)
            return false;

        if (MaxReconnectAttempts < 0 || ReconnectInterval < 0)
            return false;

        return Transport switch
        {
            MdbTransport.Tcp => !string.IsNullOrWhiteSpace(Host) && Port is >= 1 and <= 65535,
            MdbTransport.Rtu => !string.IsNullOrWhiteSpace(SerialPort) && BaudRate > 0,
            _ => false
        };
    }
}
