namespace Kwy.Device.MotionCards.Googol;

/// <summary>
/// Defines an application-level interpolation coordinate system for Kwy.
/// </summary>
/// <remarks>
/// This model combines configured machine axes and applies interpolation limits in engineering
/// units. It is consumed by Kwy at runtime and is not read from or written to <c>gts.cfg</c>.
/// </remarks>
public sealed class GoogolCoordinateSystemConfig
{
    /// <summary>Gets or sets the 1-based coordinate-system number.</summary>
    public short CoordinateSystem { get; set; }

    /// <summary>Gets or sets the ordered axis numbers that form this coordinate system.</summary>
    public short[] Axes { get; set; } = Array.Empty<short>();

    /// <summary>Gets or sets the maximum composite velocity in the shared engineering unit.</summary>
    public double MaximumVelocity { get; set; } = 500;

    /// <summary>Gets or sets the maximum composite acceleration in the shared engineering unit.</summary>
    public double MaximumAcceleration { get; set; } = 2;

    /// <summary>Gets or sets the interpolation smoothing time passed to the controller.</summary>
    public short SmoothingTime { get; set; } = 50;

    public bool Validate(short axisCount, short maximumCoordinateSystem)
        => CoordinateSystem is >= 1
            && CoordinateSystem <= maximumCoordinateSystem
            && Axes.Length is >= 2 and <= 4
            && Axes.All(axis => axis >= 1 && axis <= axisCount)
            && Axes.Distinct().Count() == Axes.Length
            && double.IsFinite(MaximumVelocity) && MaximumVelocity > 0
            && double.IsFinite(MaximumAcceleration) && MaximumAcceleration > 0
            && SmoothingTime >= 0;
}
