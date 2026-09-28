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
    /// 1730U 在 DAQNavi 中使用的设备描述，例如 "PCI-1730U,BID#0"。
    /// </summary>
    public string DeviceDescription { get; set; } = "PCI-1730U,BID#0";

    /// <summary>
    /// Device model name exposed by Kwy.
    /// </summary>
    public string Model { get; set; } = "PCI-1730U";

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
            && !string.IsNullOrWhiteSpace(Model)
            && DiPortCount is >= 1 and <= MaxSupportedPorts
            && DoPortCount is >= 1 and <= MaxSupportedPorts
            && (!EnableInterrupt || InterruptChannel >= 0 && InterruptChannel < DiPortCount * 8);
    }
}
