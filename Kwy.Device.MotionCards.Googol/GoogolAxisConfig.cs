using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.MotionCards.Googol;

/// <summary>
/// Defines the application-level semantics and machine limits of one Googol axis.
/// </summary>
/// <remarks>
/// The controller channel mapping, encoder mapping, limit/alarm input mapping, polarity,
/// filters, and control-loop parameters belong to the vendor <c>gts.cfg</c> file. This model
/// is consumed by Kwy and is not written to that file.
/// </remarks>
public sealed class GoogolAxisConfig
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
    public GoogolHomeConfig Home { get; set; } = new();

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
/// <remarks>
/// Home input mapping, polarity, and filtering remain vendor-controller settings in
/// <c>gts.cfg</c>. This model controls how Kwy executes and waits for the homing operation.
/// </remarks>
public sealed class GoogolHomeConfig
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

    public bool Validate()
        => double.IsFinite(Position)
            && double.IsFinite(Velocity) && Velocity != 0
            && double.IsFinite(Acceleration) && Acceleration > 0
            && double.IsFinite(Offset)
            && Timeout > TimeSpan.Zero;
}
