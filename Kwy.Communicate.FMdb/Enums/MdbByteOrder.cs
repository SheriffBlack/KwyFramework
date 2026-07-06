namespace Kwy.Communicate.FMdb.Enums;

/// <summary>
/// Byte layout used by the Modbus device.
/// </summary>
public enum MdbByteOrder
{
    /// <summary>
    /// Least significant byte first.
    /// </summary>
    LittleEndian,

    /// <summary>
    /// Most significant byte first.
    /// </summary>
    BigEndian
}
