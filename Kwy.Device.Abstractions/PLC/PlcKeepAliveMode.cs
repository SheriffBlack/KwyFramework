namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC heartbeat read mode.
/// </summary>
public enum PlcKeepAliveMode
{
    /// <summary>
    /// Read a Boolean address.
    /// </summary>
    ReadBool,

    /// <summary>
    /// Read a 16-bit integer address.
    /// </summary>
    ReadInt16,

    /// <summary>
    /// Read a 32-bit integer address.
    /// </summary>
    ReadInt32,

    /// <summary>
    /// Read a 32-bit floating-point address.
    /// </summary>
    ReadFloat,

    /// <summary>
    /// Read one byte/word block from the address.
    /// </summary>
    ReadBytes
}
