using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.MotionCards.Leadshine;

/// <summary>
/// Defines how Kwy opens a Leadshine controller and how the application uses the machine axes.
/// </summary>
public sealed class LeadshineMotionCardConfig : IDeviceConfig
{
    public const short SupportedAxisCount = 8;

    /// <summary>
    /// Maximum logical IO width exposed by Kwy. LTDMC ports are combined as two 32-bit values.
    /// </summary>
    public const int MaxSupportedIoChannelCount = 64;
    public const short MaxSupportedCoordinateSystemCount = 2;

    public short CardNo { get; set; }

    /// <summary>Optional stable registry key. Defaults to <c>Leadshine-{CardNo}</c>.</summary>
    public string? DeviceId { get; set; }

    public string Model => "DMC3800";

    /// <summary>
    /// Vendor controller configuration loaded by dmc_download_configfile.
    /// </summary>
    public string? ConfigFilePath { get; set; } = "dmc.cfg";

    public bool ResetOnConnect { get; set; } = true;

    public bool LoadConfigOnConnect { get; set; } = true;

    public short AxisCount => SupportedAxisCount;

    public short DiChannelCount { get; set; } = 16;

    public short DoChannelCount { get; set; } = 16;

    /// <summary>按轴通道保存雷赛控制器专属参数。</summary>
    public IDictionary<short, LeadshineAxisOptions> AxisOptions { get; }
        = new Dictionary<short, LeadshineAxisOptions>();

    /// <summary>
    /// Gets the application-level coordinate-system definitions used for interpolation.
    /// </summary>
    public IList<LeadshineCoordinateSystemConfig> CoordinateSystems { get; } = new List<LeadshineCoordinateSystemConfig>();

    /// <summary>
    /// Leadshine common IO active polarity configuration.
    /// </summary>
    public bool DigitalIoActiveLow { get; set; } = true;

    public bool Validate()
    {
        string resolvedDeviceId = DeviceId ?? $"Leadshine-{CardNo}";
        return CardNo >= 0
            && !string.IsNullOrWhiteSpace(resolvedDeviceId)
            && (!LoadConfigOnConnect || !string.IsNullOrWhiteSpace(ConfigFilePath))
            && DiChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && DoChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && AxisOptions.Keys.All(axis => axis >= 1 && axis <= AxisCount)
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public LeadshineAxisOptions GetAxisOptions(short axis)
    {
        return AxisOptions.TryGetValue(axis, out LeadshineAxisOptions? options)
            ? options
            : new LeadshineAxisOptions();
    }

    public LeadshineCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

}
