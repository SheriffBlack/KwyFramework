using Kwy.Device.Abstractions;

namespace Kwy.Device.IoCards.Advantech;

/// <summary>
/// Advantech digital IO card configuration.
/// </summary>
public sealed class AdvantechIoCardConfig : IDeviceConfig
{
    public const int MaxSupportedChannels = 64;
    public const int MaxSupportedPorts = MaxSupportedChannels / 8;

    /// <summary>
    /// Device description used by Advantech DAQNavi, for example "PCI-1730,BID#0".
    /// </summary>
    public string DeviceDescription { get; set; } = "PCI-1730,BID#0";

    /// <summary>
    /// Device model name exposed by Kwy.
    /// </summary>
    public string Model { get; set; } = "PCI-1730";

    /// <summary>
    /// Digital input port count. One port contains eight channels.
    /// </summary>
    public int DiPortCount { get; set; } = MaxSupportedPorts;

    /// <summary>
    /// Digital output port count. One port contains eight channels.
    /// </summary>
    public int DoPortCount { get; set; } = MaxSupportedPorts;

    /// <summary>
    /// Enables DAQNavi snapshot interrupt listening.
    /// </summary>
    public bool EnableInterrupt { get; set; } = true;

    /// <summary>
    /// Interrupt source channel index.
    /// </summary>
    public int InterruptChannel { get; set; }

    /// <summary>
    /// Uses rising edge trigger for interrupt; otherwise falling edge.
    /// </summary>
    public bool InterruptRisingEdge { get; set; } = true;

    public bool Validate()
    {
        return !string.IsNullOrWhiteSpace(DeviceDescription)
            && DiPortCount is >= 1 and <= MaxSupportedPorts
            && DoPortCount is >= 1 and <= MaxSupportedPorts
            && InterruptChannel is >= 0 and < MaxSupportedChannels;
    }
}
