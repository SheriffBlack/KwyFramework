namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// PLC protocol-level heartbeat configuration.
/// </summary>
public interface IPlcKeepAliveConfig
{
    /// <summary>
    /// Whether to enable PLC protocol-level heartbeat.
    /// </summary>
    bool KeepAlive { get; set; }

    /// <summary>
    /// PLC heartbeat interval in milliseconds.
    /// </summary>
    int KeepAliveInterval { get; set; }

    /// <summary>
    /// Address used for heartbeat read.
    /// </summary>
    string? KeepAliveAddress { get; set; }

    /// <summary>
    /// Data read mode for the heartbeat address.
    /// </summary>
    PlcKeepAliveMode KeepAliveMode { get; set; }
}
