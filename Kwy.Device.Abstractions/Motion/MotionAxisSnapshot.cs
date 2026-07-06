namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// Immutable axis state snapshot.
/// </summary>
public readonly record struct MotionAxisSnapshot
{
    public MotionAxisSnapshot(
        short axis,
        double position,
        double encoderPosition,
        double velocity,
        int rawStatus,
        bool isMoving,
        bool isAlarm,
        bool isPositiveLimit,
        bool isNegativeLimit,
        DateTimeOffset timestamp,
        bool isServoEnabled = false,
        HomeState homeState = HomeState.Unknown)
    {
        Axis = axis;
        Position = position;
        EncoderPosition = encoderPosition;
        Velocity = velocity;
        RawStatus = rawStatus;
        IsMoving = isMoving;
        IsAlarm = isAlarm;
        IsPositiveLimit = isPositiveLimit;
        IsNegativeLimit = isNegativeLimit;
        Timestamp = timestamp;
        IsServoEnabled = isServoEnabled;
        HomeState = homeState;
    }

    public short Axis { get; }

    public double Position { get; }

    public double EncoderPosition { get; }

    public double Velocity { get; }

    public int RawStatus { get; }

    public bool IsMoving { get; }

    public bool IsAlarm { get; }

    public bool IsPositiveLimit { get; }

    public bool IsNegativeLimit { get; }

    public DateTimeOffset Timestamp { get; }

    public bool IsServoEnabled { get; }

    public HomeState HomeState { get; }

    public bool HasSameState(MotionAxisSnapshot other)
    {
        return Axis == other.Axis
            && Position.Equals(other.Position)
            && EncoderPosition.Equals(other.EncoderPosition)
            && Velocity.Equals(other.Velocity)
            && RawStatus == other.RawStatus
            && IsMoving == other.IsMoving
            && IsAlarm == other.IsAlarm
            && IsPositiveLimit == other.IsPositiveLimit
            && IsNegativeLimit == other.IsNegativeLimit
            && IsServoEnabled == other.IsServoEnabled
            && HomeState == other.HomeState;
    }
}
