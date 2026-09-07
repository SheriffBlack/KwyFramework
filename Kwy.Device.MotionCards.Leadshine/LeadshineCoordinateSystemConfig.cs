namespace Kwy.Device.MotionCards.Leadshine;

/// <summary>
/// Defines an application-level interpolation coordinate system for Kwy using Leadshine hardware.
/// </summary>
public sealed class LeadshineCoordinateSystemConfig
{
    /// <summary>Gets or sets the 1-based coordinate-system number.</summary>
    public short CoordinateSystem { get; set; }

    /// <summary>Gets or sets the ordered axis numbers that form this coordinate system.</summary>
    public short[] Axes { get; set; } = Array.Empty<short>();

    /// <summary>Gets or sets the maximum composite velocity in the shared engineering unit.</summary>
    public double MaximumVelocity { get; set; } = 500;

    /// <summary>Gets or sets the maximum composite acceleration in the shared engineering unit.</summary>
    public double MaximumAcceleration { get; set; } = 2;

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length
            && double.IsFinite(MaximumVelocity) && MaximumVelocity > 0
            && double.IsFinite(MaximumAcceleration) && MaximumAcceleration > 0;
}
