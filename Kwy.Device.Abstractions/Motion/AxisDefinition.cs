namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// Identifies one physical axis independently of a vendor-specific card model.
/// </summary>
public readonly record struct AxisId(string DeviceId, short Channel)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        if (Channel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Channel), Channel, "Channel must be greater than or equal to 1.");
        }
    }
}

public sealed record AxisLimitConfig
{
    public double MinimumPosition { get; init; } = double.NegativeInfinity;
    public double MaximumPosition { get; init; } = double.PositiveInfinity;
    public double MaximumVelocity { get; init; } = double.PositiveInfinity;
    public double MaximumAcceleration { get; init; } = double.PositiveInfinity;
    public double MaximumDeceleration { get; init; } = double.PositiveInfinity;
    public double? MaximumJerk { get; init; }

    public void Validate()
    {
        if (double.IsNaN(MinimumPosition) || double.IsNaN(MaximumPosition) || MinimumPosition >= MaximumPosition)
            throw new ArgumentException("MinimumPosition must be less than MaximumPosition.");
        ValidatePositiveLimit(MaximumVelocity, nameof(MaximumVelocity));
        ValidatePositiveLimit(MaximumAcceleration, nameof(MaximumAcceleration));
        ValidatePositiveLimit(MaximumDeceleration, nameof(MaximumDeceleration));
        if (MaximumJerk is { } jerk) ValidatePositiveLimit(jerk, nameof(MaximumJerk));
    }

    private static void ValidatePositiveLimit(double value, string name)
    {
        if (double.IsNaN(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be greater than 0.");
    }
}

public sealed record AxisMotionDefaults
{
    public double PositionTolerance { get; init; } = 0.01;
    public TimeSpan MotionTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SettlingTime { get; init; } = TimeSpan.Zero;
    public double? SettlingVelocityThreshold { get; init; }
    public double? FollowingErrorLimit { get; init; }

    public void Validate()
    {
        if (!double.IsFinite(PositionTolerance) || PositionTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(PositionTolerance));
        if (MotionTimeout <= TimeSpan.Zero && MotionTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(MotionTimeout));
        if (SettlingTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(SettlingTime));
        ValidateOptionalNonNegative(SettlingVelocityThreshold, nameof(SettlingVelocityThreshold));
        ValidateOptionalNonNegative(FollowingErrorLimit, nameof(FollowingErrorLimit));
    }

    public MotionExecutionOptions ToExecutionOptions() => new()
    {
        PositionTolerance = PositionTolerance,
        Timeout = MotionTimeout,
        SettlingTime = SettlingTime,
        SettlingVelocityThreshold = SettlingVelocityThreshold,
        FollowingErrorLimit = FollowingErrorLimit
    };

    private static void ValidateOptionalNonNegative(double? value, string name)
    {
        if (value is { } number && (!double.IsFinite(number) || number < 0))
            throw new ArgumentOutOfRangeException(name, number, $"{name} must be finite and non-negative.");
    }
}

public sealed record AxisHomeDefinition
{
    public bool Enabled { get; init; } = true;
    public double Position { get; init; }
    public int Mode { get; init; }
    public int Direction { get; init; } = -1;
    public double SearchVelocity { get; init; } = 10;
    public double CreepVelocity { get; init; } = 1;
    public double Acceleration { get; init; } = 100;
    public double Offset { get; init; }
    public bool UseIndexSignal { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    public void Validate()
    {
        if (!double.IsFinite(Position)) throw new ArgumentOutOfRangeException(nameof(Position));
        if (Direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(Direction));
        ValidatePositive(SearchVelocity, nameof(SearchVelocity));
        ValidatePositive(CreepVelocity, nameof(CreepVelocity));
        if (CreepVelocity > SearchVelocity)
            throw new ArgumentException("CreepVelocity must not exceed SearchVelocity.");
        ValidatePositive(Acceleration, nameof(Acceleration));
        if (!double.IsFinite(Offset)) throw new ArgumentOutOfRangeException(nameof(Offset));
        if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(Timeout));
    }

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and greater than 0.");
    }
}

public enum AxisFeedbackSource
{
    MotorEncoder,
    LoadEncoder,
    LinearScale
}

public readonly record struct AxisForbiddenRange(double Minimum, double Maximum)
{
    public bool Contains(double position) => position >= Minimum && position <= Maximum;

    public void Validate()
    {
        if (!double.IsFinite(Minimum) || !double.IsFinite(Maximum) || Minimum >= Maximum)
            throw new ArgumentException("Forbidden range minimum must be less than maximum.");
    }
}

public enum AxisBrakeDisableStrategy
{
    EngageBeforeServoOff,
    ServoOffBeforeEngage
}

public sealed record AxisSafetyDefinition
{
    public bool RequireHomedBeforeMotion { get; init; } = true;
    public bool IsVerticalAxis { get; init; }
    public bool HasBrake { get; init; }
    public TimeSpan BrakeReleaseDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan BrakeEngageDelay { get; init; } = TimeSpan.Zero;
    public AxisBrakeDisableStrategy BrakeDisableStrategy { get; init; } = AxisBrakeDisableStrategy.EngageBeforeServoOff;
    public double GravityCompensation { get; init; }
    public double? SafeHeight { get; init; }
    public IReadOnlyList<string> RequiredInterlocks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AxisForbiddenRange> ForbiddenRanges { get; init; } = Array.Empty<AxisForbiddenRange>();

    public void Validate()
    {
        if (!Enum.IsDefined(BrakeDisableStrategy)) throw new ArgumentOutOfRangeException(nameof(BrakeDisableStrategy));
        if (HasBrake && !IsVerticalAxis) throw new InvalidOperationException("A brake can only be declared for a vertical axis.");
        if (BrakeReleaseDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(BrakeReleaseDelay));
        if (BrakeEngageDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(BrakeEngageDelay));
        if (SafeHeight is { } height && !double.IsFinite(height)) throw new ArgumentOutOfRangeException(nameof(SafeHeight));
        if (!double.IsFinite(GravityCompensation)) throw new ArgumentOutOfRangeException(nameof(GravityCompensation));
        ArgumentNullException.ThrowIfNull(RequiredInterlocks);
        ArgumentNullException.ThrowIfNull(ForbiddenRanges);
        if (RequiredInterlocks.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Interlock identifiers cannot be empty.", nameof(RequiredInterlocks));
        foreach (AxisForbiddenRange range in ForbiddenRanges) range.Validate();
    }
}

public enum RotaryPositionMode
{
    Accumulated,
    Modulo
}

public sealed record RotaryAxisDefinition
{
    public RotaryPositionMode PositionMode { get; init; } = RotaryPositionMode.Accumulated;
    public double Period { get; init; } = 360;
    public int PreferredDirection { get; init; }
    public double? MinimumAccumulatedAngle { get; init; }
    public double? MaximumAccumulatedAngle { get; init; }
    public IReadOnlyList<AxisForbiddenRange> ForbiddenAngles { get; init; } = Array.Empty<AxisForbiddenRange>();
    public string? RotationCenterCompensationId { get; init; }
    public string? EccentricityCompensationId { get; init; }

    public bool IsForbidden(double accumulatedAngle)
    {
        double normalized = accumulatedAngle - Math.Floor(accumulatedAngle / Period) * Period;
        return ForbiddenAngles.Any(range => range.Contains(normalized));
    }

    public void Validate()
    {
        if (!Enum.IsDefined(PositionMode)) throw new ArgumentOutOfRangeException(nameof(PositionMode));
        if (!double.IsFinite(Period) || Period <= 0) throw new ArgumentOutOfRangeException(nameof(Period));
        if (PreferredDirection is not (-1 or 0 or 1)) throw new ArgumentOutOfRangeException(nameof(PreferredDirection));
        if (MinimumAccumulatedAngle is { } min && !double.IsFinite(min)) throw new ArgumentOutOfRangeException(nameof(MinimumAccumulatedAngle));
        if (MaximumAccumulatedAngle is { } max && !double.IsFinite(max)) throw new ArgumentOutOfRangeException(nameof(MaximumAccumulatedAngle));
        if (MinimumAccumulatedAngle >= MaximumAccumulatedAngle) throw new ArgumentException("MinimumAccumulatedAngle must be less than MaximumAccumulatedAngle.");
        ArgumentNullException.ThrowIfNull(ForbiddenAngles);
        foreach (AxisForbiddenRange range in ForbiddenAngles) range.Validate();
    }
}

public sealed record AxisCalibrationConfig
{
    public double MechanicalZeroOffset { get; init; }
    public double CalibrationZeroOffset { get; init; }
    public string? ErrorCompensationTableId { get; init; }
    public string? ThermalCompensationId { get; init; }
    public AxisFeedbackSource FeedbackSource { get; init; } = AxisFeedbackSource.MotorEncoder;
    public bool DualLoopEnabled { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(FeedbackSource)) throw new ArgumentOutOfRangeException(nameof(FeedbackSource));
        if (!double.IsFinite(MechanicalZeroOffset)) throw new ArgumentOutOfRangeException(nameof(MechanicalZeroOffset));
        if (!double.IsFinite(CalibrationZeroOffset)) throw new ArgumentOutOfRangeException(nameof(CalibrationZeroOffset));
        if (DualLoopEnabled && FeedbackSource == AxisFeedbackSource.MotorEncoder)
            throw new InvalidOperationException("Dual-loop control requires a load encoder or linear scale.");
    }
}

/// <summary>
/// Complete application-level definition of an axis. Vendor-specific electrical
/// and controller parameters remain in the motion-card implementation.
/// </summary>
public sealed record AxisDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string DeviceId { get; init; }
    public short Channel { get; init; }
    public required AxisEngineeringConfig Engineering { get; init; }
    public AxisLimitConfig Limits { get; init; } = new();
    public AxisMotionDefaults Defaults { get; init; } = new();
    public AxisHomeDefinition Home { get; init; } = new();
    public AxisSafetyDefinition Safety { get; init; } = new();
    public RotaryAxisDefinition? Rotary { get; init; }
    public AxisCalibrationConfig Calibration { get; init; } = new();

    public AxisId PhysicalId => new(DeviceId, Channel);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentNullException.ThrowIfNull(Engineering);
        ArgumentNullException.ThrowIfNull(Limits);
        ArgumentNullException.ThrowIfNull(Defaults);
        ArgumentNullException.ThrowIfNull(Home);
        ArgumentNullException.ThrowIfNull(Safety);
        ArgumentNullException.ThrowIfNull(Calibration);
        PhysicalId.Validate();
        Engineering.Validate();
        Limits.Validate();
        Defaults.Validate();
        Home.Validate();
        Safety.Validate();
        Calibration.Validate();
        Rotary?.Validate();
        if (Rotary is not null && Engineering.Unit != MotionUnit.Degree)
            throw new InvalidOperationException("Rotary axis settings require MotionUnit.Degree.");
    }
}
