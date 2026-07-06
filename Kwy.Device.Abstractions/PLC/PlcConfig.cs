using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC connection transport.
/// </summary>
public enum PlcConnectionTransport
{
    Tcp,
    Serial
}

/// <summary>
/// Common PLC connection configuration.
/// Vendor-specific PLC modules can extend this class with protocol-specific fields.
/// </summary>
public class PlcConfig : IDeviceConfig, IPlcKeepAliveConfig
{
    /// <summary>
    /// Gets or sets the PLC connection transport.
    /// </summary>
    public PlcConnectionTransport Transport { get; set; } = PlcConnectionTransport.Tcp;

    /// <summary>
    /// Gets or sets the PLC IP address when <see cref="Transport"/> is <see cref="PlcConnectionTransport.Tcp"/>.
    /// </summary>
    public string IpAddress { get; set; } = "192.168.0.10";

    /// <summary>
    /// Gets or sets the target TCP port. Vendor modules can apply protocol defaults when this value is 0.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the serial port name when <see cref="Transport"/> is <see cref="PlcConnectionTransport.Serial"/>.
    /// </summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>
    /// Gets or sets the serial baud rate.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Gets or sets the serial data bits.
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Gets or sets the serial parity.
    /// </summary>
    public ParityType Parity { get; set; } = ParityType.None;

    /// <summary>
    /// Gets or sets the serial stop bits.
    /// </summary>
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    /// <summary>
    /// Gets or sets whether PLC protocol-level keep-alive is enabled.
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// Gets or sets the PLC keep-alive interval in milliseconds.
    /// </summary>
    public int KeepAliveInterval { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the PLC keep-alive read address. Empty value disables active keep-alive reads.
    /// </summary>
    public string? KeepAliveAddress { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive address read type.
    /// </summary>
    public PlcKeepAliveMode KeepAliveMode { get; set; } = PlcKeepAliveMode.ReadBool;

    public virtual bool Validate()
    {
        if (Transport == PlcConnectionTransport.Tcp && string.IsNullOrWhiteSpace(IpAddress))
        {
            return false;
        }

        if (Transport == PlcConnectionTransport.Serial)
        {
            if (string.IsNullOrWhiteSpace(PortName) || BaudRate <= 0 || DataBits is < 5 or > 8)
            {
                return false;
            }
        }

        if (KeepAlive && KeepAliveInterval <= 0)
        {
            return false;
        }

        return true;
    }
}
