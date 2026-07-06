using Kwy.Device.Abstractions;

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

    /// <summary>
    /// Gets the machine-level axis definitions used by Kwy for engineering-unit conversion,
    /// software travel limits, motion limits, direction conversion, and homing behavior.
    /// </summary>
    public IList<LeadshineAxisConfig> Axes { get; } = new List<LeadshineAxisConfig>();

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
        return CardNo >= 0
            && DiChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && DoChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && Axes.All(item => item.Validate(AxisCount))
            && Axes.Select(item => item.Axis).Distinct().Count() == Axes.Count
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.All(HasCompatibleCoordinateUnits)
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public LeadshineAxisConfig GetAxisConfig(short axis)
        => Axes.FirstOrDefault(item => item.Axis == axis)
            ?? new LeadshineAxisConfig { Axis = axis, Name = $"Axis {axis}" };

    public LeadshineCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

    private bool HasCompatibleCoordinateUnits(LeadshineCoordinateSystemConfig coordinateSystem)
    {
        LeadshineAxisConfig first = GetAxisConfig(coordinateSystem.Axes[0]);
        return coordinateSystem.Axes.Skip(1)
            .Select(GetAxisConfig)
            .All(item => item.Unit == first.Unit && item.PulsesPerUnit.Equals(first.PulsesPerUnit));
    }
}
