using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.MotionCards.Leadshine;

/// <summary>
/// Defines the application-level semantics and machine limits of one Leadshine axis.
/// </summary>
public sealed class LeadshineAxisConfig
{
    /// <summary>Gets or sets the 1-based controller axis number.</summary>
    public short Axis { get; set; }

    /// <summary>Gets or sets the machine-facing axis name, such as X, Y, or Z.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the engineering unit exposed to application code.</summary>
    public MotionUnit Unit { get; set; } = MotionUnit.Pulse;

    /// <summary>Gets or sets the number of controller pulses represented by one engineering unit.</summary>
    public double PulsesPerUnit { get; set; } = 1;

    /// <summary>Gets or sets whether Kwy reverses the application coordinate direction.</summary>
    public bool DirectionReversed { get; set; }

    /// <summary>Gets or sets the optional application software minimum position.</summary>
    public double? MinimumPosition { get; set; }

    /// <summary>Gets or sets the optional application software maximum position.</summary>
    public double? MaximumPosition { get; set; }

    /// <summary>Gets or sets the optional application velocity limit in engineering units.</summary>
    public double? MaximumVelocity { get; set; }

    /// <summary>Gets or sets the optional application acceleration limit in engineering units.</summary>
    public double? MaximumAcceleration { get; set; }

    /// <summary>Gets or sets the optional application deceleration limit in engineering units.</summary>
    public double? MaximumDeceleration { get; set; }

    /// <summary>Gets or sets the Kwy homing recipe for this axis.</summary>
    public LeadshineHomeConfig Home { get; set; } = new();

    public AxisEngineeringConfig ToEngineeringConfig()
        => new()
        {
            Axis = Axis,
            Unit = Unit,
            PulsesPerUnit = PulsesPerUnit,
            DirectionReversed = DirectionReversed
        };

    public bool Validate(short axisCount)
    {
        try
        {
            ToEngineeringConfig().Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }

        return Axis <= axisCount
            && IsFiniteOrNull(MinimumPosition)
            && IsFiniteOrNull(MaximumPosition)
            && (MinimumPosition is null || MaximumPosition is null || MinimumPosition < MaximumPosition)
            && IsPositiveOrNull(MaximumVelocity)
            && IsPositiveOrNull(MaximumAcceleration)
            && IsPositiveOrNull(MaximumDeceleration)
            && Home.Validate();
    }

    private static bool IsFiniteOrNull(double? value) => value is null || double.IsFinite(value.Value);

    private static bool IsPositiveOrNull(double? value)
        => value is null || double.IsFinite(value.Value) && value.Value > 0;
}

/// <summary>
/// Defines the application homing recipe for one axis.
/// </summary>
public sealed class LeadshineHomeConfig
{
    /// <summary>Gets or sets whether Kwy permits homing for this axis.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the application position assigned after homing completes.</summary>
    public double Position { get; set; }

    /// <summary>Gets or sets the homing velocity in the axis engineering unit.</summary>
    public double Velocity { get; set; } = 20;

    /// <summary>Gets or sets the homing acceleration in the axis engineering unit.</summary>
    public double Acceleration { get; set; } = 0.5;

    /// <summary>Gets or sets the machine offset applied after the home signal is found.</summary>
    public double Offset { get; set; }

    /// <summary>Gets or sets the maximum time Kwy waits for homing to complete.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Homing mode for Leadshine dmc_set_homemode.
    /// Default is 1 (ORG only), but can be configured for other modes supported by LTDMC.
    /// </summary>
    public ushort HomeMode { get; set; } = 1;

    /// <summary>
    /// EZ count for homing. Typically used when HomeMode uses EZ index signal.
    /// </summary>
    public ushort EzCount { get; set; } = 0;

    public bool Validate()
        => double.IsFinite(Position)
            && double.IsFinite(Velocity) && Velocity != 0
            && double.IsFinite(Acceleration) && Acceleration > 0
            && double.IsFinite(Offset)
            && Timeout > TimeSpan.Zero;
}
