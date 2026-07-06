using Kwy.Device.Abstractions;

namespace Kwy.Device.MotionCards.Googol;

/// <summary>
/// Defines how Kwy opens a Googol controller and how the application uses the machine axes.
/// </summary>
/// <remarks>
/// This model does not replace the vendor <c>gts.cfg</c> file. The vendor file contains
/// controller-level hardware and electrical settings and is loaded by <c>GT_LoadConfig</c>.
/// <see cref="Axes"/> and <see cref="CoordinateSystems"/> contain Kwy application settings;
/// they are interpreted at runtime and are never written to <c>gts.cfg</c>.
/// </remarks>
public sealed class GoogolMotionCardConfig : IDeviceConfig
{
    public const int MaxSupportedAxisCount = 8;
    // GT_GetDi/GT_GetDo expose one 32-bit common-I/O image. Channels above 31
    // require a different vendor API and must not be emulated with wrapped shifts.
    public const int MaxSupportedIoChannelCount = 32;
    public const short MaxSupportedCoordinateSystemCount = 2;

    public short CardNo { get; set; }

    /// <summary>Optional stable registry key. Defaults to <c>Googol-{CardNo}</c>.</summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// SDK communication channel passed to GT_Open. GTS pulse controllers commonly use channel 0.
    /// </summary>
    public short OpenChannel { get; set; } = 0;

    /// <summary>
    /// SDK open parameter passed to GT_Open. The vendor default is 1.
    /// </summary>
    public short OpenParameter { get; set; } = 1;

    public string Model { get; set; } = "GTS-800";

    /// <summary>
    /// Vendor controller configuration loaded by GT_LoadConfig. It contains hardware mapping,
    /// electrical polarity, filters, control loops, and other GTS controller parameters.
    /// </summary>
    /// <remarks>
    /// The file is parsed and applied by the Googol SDK. Kwy only passes this path to
    /// <c>GT_LoadConfig</c> and does not merge the C# axis or coordinate-system settings into it.
    /// </remarks>
    public string? ConfigFilePath { get; set; } = "gts.cfg";

    public bool ResetOnConnect { get; set; } = true;

    public bool LoadConfigOnConnect { get; set; } = true;

    public short AxisCount { get; set; } = 8;

    public short DiChannelCount { get; set; } = 16;

    public short DoChannelCount { get; set; } = 16;

    /// <summary>
    /// Gets the machine-level axis definitions used by Kwy for engineering-unit conversion,
    /// software travel limits, motion limits, direction conversion, and homing behavior.
    /// </summary>
    /// <remarks>These values are not read from or written to <c>gts.cfg</c>.</remarks>
    public IList<GoogolAxisConfig> Axes { get; } = new List<GoogolAxisConfig>();

    /// <summary>
    /// Gets the application-level coordinate-system definitions used for interpolation.
    /// </summary>
    /// <remarks>
    /// These definitions describe which configured machine axes form a Kwy coordinate system
    /// and its application motion limits. They are not stored in <c>gts.cfg</c>.
    /// </remarks>
    public IList<GoogolCoordinateSystemConfig> CoordinateSystems { get; } = new List<GoogolCoordinateSystemConfig>();

    /// <summary>
    /// GTS common IO is usually active-low: active=true maps to physical bit 0.
    /// </summary>
    public bool DigitalIoActiveLow { get; set; } = true;

    public bool Validate()
    {
        return AxisCount is >= 1 and <= MaxSupportedAxisCount
            && DiChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && DoChannelCount is >= 1 and <= MaxSupportedIoChannelCount
            && Axes.All(item => item.Validate(AxisCount))
            && Axes.Select(item => item.Axis).Distinct().Count() == Axes.Count
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.All(HasCompatibleCoordinateUnits)
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public GoogolAxisConfig GetAxisConfig(short axis)
        => Axes.FirstOrDefault(item => item.Axis == axis)
            ?? new GoogolAxisConfig { Axis = axis, Name = $"Axis {axis}" };

    public GoogolCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

    private bool HasCompatibleCoordinateUnits(GoogolCoordinateSystemConfig coordinateSystem)
    {
        GoogolAxisConfig first = GetAxisConfig(coordinateSystem.Axes[0]);
        return coordinateSystem.Axes.Skip(1)
            .Select(GetAxisConfig)
            .All(item => item.Unit == first.Unit && item.PulsesPerUnit.Equals(first.PulsesPerUnit));
    }
}
