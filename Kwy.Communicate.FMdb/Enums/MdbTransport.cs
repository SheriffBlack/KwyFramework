namespace Kwy.Communicate.FMdb.Enums;

/// <summary>
/// FluentModbus transport type.
/// </summary>
public enum MdbTransport
{
    /// <summary>
    /// Modbus TCP over Ethernet.
    /// </summary>
    Tcp,

    /// <summary>
    /// Modbus RTU over a serial port.
    /// </summary>
    Rtu
}
