namespace Kwy.Device.Abstractions.Motion;

public enum MotionUnit
{
    Pulse,
    Millimeter,
    Degree
}

public sealed record AxisEngineeringConfig
{
    public short Axis { get; init; }

    public MotionUnit Unit { get; init; } = MotionUnit.Pulse;

    public double PulsesPerUnit { get; init; } = 1;

    public bool DirectionReversed { get; init; }

    public void Validate()
    {
        if (Axis < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Axis), Axis, "Axis must be greater than or equal to 1.");
        }

        if (!double.IsFinite(PulsesPerUnit) || PulsesPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PulsesPerUnit), PulsesPerUnit, "PulsesPerUnit must be finite and greater than 0.");
        }
    }
}

public sealed record MotionProfile
{
    public MotionProfile(double velocity, double acceleration, double deceleration)
    {
        if (!double.IsFinite(velocity) || velocity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "Velocity must be finite and greater than 0.");
        }

        if (!double.IsFinite(acceleration) || acceleration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(acceleration), acceleration, "Acceleration must be finite and greater than 0.");
        }

        if (!double.IsFinite(deceleration) || deceleration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deceleration), deceleration, "Deceleration must be finite and greater than 0.");
        }

        Velocity = velocity;
        Acceleration = acceleration;
        Deceleration = deceleration;
    }

    public double Velocity { get; }

    public double Acceleration { get; }

    public double Deceleration { get; }
}

public enum HomeState
{
    Unknown,
    Idle,
    Running,
    Succeeded,
    Failed
}

public readonly record struct HomeStatus(
    short Axis,
    HomeState State,
    int RawStatus,
    string? ErrorMessage = null)
{
    public bool IsCompleted => State is HomeState.Succeeded or HomeState.Failed;
}

/// <summary>
/// Describes a successfully completed point-to-point axis motion.
/// </summary>
public readonly record struct MotionCompletionResult(
    short Axis,
    double TargetPosition,
    double ActualPosition,
    double Tolerance)
{
    public double PositionError => ActualPosition - TargetPosition;
}

public sealed class MotionExecutionOptions
{
    public double PositionTolerance { get; set; } = 0.01;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool StopOnCancellation { get; set; } = true;

    public TimeSpan StartDetectionDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    public void Validate()
    {
        if (!double.IsFinite(PositionTolerance) || PositionTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PositionTolerance));
        }

        if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        if (StartDetectionDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(StartDetectionDelay));
        }
    }
}

public enum PositionCrossingDirection
{
    Positive,
    Negative
}

public enum SensorStopMode
{
    ControllerHardwareStop,
    SoftwareStop
}

public sealed class SensorSeekOptions
{
    public bool ExpectedState { get; set; } = true;

    public SensorStopMode StopMode { get; set; } = SensorStopMode.ControllerHardwareStop;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    public bool AbortOnCancellation { get; set; } = true;

    public void Validate()
    {
        if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }

        if (PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(PollInterval));
        }
    }
}

public readonly record struct SensorSeekResult(
    short Axis,
    int Channel,
    double Position,
    SensorStopMode StopMode);

public enum MotionRequestKind
{
    Absolute,
    Relative,
    Jog,
    Home
}

public readonly record struct MotionRequest(
    short Axis,
    MotionRequestKind Kind,
    double? TargetPosition = null,
    int Direction = 0,
    bool RequiresHomed = true);

public sealed record MotionSafetyViolation(string Code, string Message);

public sealed record MotionSafetyResult(IReadOnlyList<MotionSafetyViolation> Violations)
{
    public bool IsAllowed => Violations.Count == 0;

    public static MotionSafetyResult Allowed { get; } = new(Array.Empty<MotionSafetyViolation>());
}

public sealed record NamedPositionSet(
    string Name,
    IReadOnlyDictionary<short, double> Positions,
    string? ProfileName = null);
