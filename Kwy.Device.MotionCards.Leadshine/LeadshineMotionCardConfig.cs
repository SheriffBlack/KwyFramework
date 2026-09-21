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

    /// <summary>
    /// Gets the machine-level axis definitions used by Kwy for engineering-unit conversion,
    /// software travel limits, motion limits, direction conversion, and homing behavior.
    /// </summary>
    public IList<AxisDefinition> Axes { get; } = new List<AxisDefinition>();

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
            && Axes.Count > 0
            && Axes.All(IsValidAxis)
            && Axes.Select(item => item.Channel).Distinct().Count() == Axes.Count
            && Axes.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Axes.Count
            && Axes.All(item => string.Equals(item.DeviceId, resolvedDeviceId, StringComparison.OrdinalIgnoreCase))
            && AxisOptions.Keys.All(axis => axis >= 1 && axis <= AxisCount)
            && AxisOptions.Keys.All(axis => Axes.Any(definition => definition.Channel == axis))
            && CoordinateSystems.All(item => item.Validate(AxisCount, MaxSupportedCoordinateSystemCount))
            && CoordinateSystems.All(item => item.Axes.All(axis => Axes.Any(definition => definition.Channel == axis)))
            && CoordinateSystems.All(HasCompatibleCoordinateUnits)
            && CoordinateSystems.Select(item => item.CoordinateSystem).Distinct().Count() == CoordinateSystems.Count;
    }

    public AxisDefinition GetAxisDefinition(short axis)
        => Axes.FirstOrDefault(item => item.Channel == axis)
            ?? throw new KeyNotFoundException($"Axis {axis} is not configured.");

    public LeadshineAxisOptions GetAxisOptions(short axis)
    {
        _ = GetAxisDefinition(axis);
        return AxisOptions.TryGetValue(axis, out LeadshineAxisOptions? options)
            ? options
            : new LeadshineAxisOptions();
    }

    public LeadshineCoordinateSystemConfig? GetCoordinateSystemConfig(short coordinateSystem)
        => CoordinateSystems.FirstOrDefault(item => item.CoordinateSystem == coordinateSystem);

    private bool HasCompatibleCoordinateUnits(LeadshineCoordinateSystemConfig coordinateSystem)
    {
        AxisEngineeringConfig first = GetAxisDefinition(coordinateSystem.Axes[0]).Engineering;
        return coordinateSystem.Axes.Skip(1)
            .Select(axis => GetAxisDefinition(axis).Engineering)
            .All(item => item.Unit == first.Unit && item.PulsesPerUnit.Equals(first.PulsesPerUnit));
    }

    private bool IsValidAxis(AxisDefinition definition)
    {
        try
        {
            definition.Validate();
            return definition.Channel <= AxisCount;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
