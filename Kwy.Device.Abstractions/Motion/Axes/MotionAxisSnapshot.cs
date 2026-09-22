namespace Kwy.Device.Abstractions.Motion;

/// <summary>某一时刻的物理轴不可变快照；用于监视、诊断和执行器闭环，不作为业务配置。</summary>
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
        HomeState homeState = HomeState.Unknown,
        AxisFault? fault = null)
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
        Fault = fault;
    }

    /// <summary>控制卡内物理轴通道号。</summary>
    public short Axis { get; }

    /// <summary>控制器规划位置，单位为轴工程单位。</summary>
    public double Position { get; }

    /// <summary>编码器反馈位置，用于跟随误差和精密到位诊断。</summary>
    public double EncoderPosition { get; }

    public double Velocity { get; }

    /// <summary>厂商原始状态字，仅供底层映射与诊断，不应由工艺层判断位含义。</summary>
    public int RawStatus { get; }

    public bool IsMoving { get; }

    public bool IsAlarm { get; }

    public bool IsPositiveLimit { get; }

    public bool IsNegativeLimit { get; }

    public DateTimeOffset Timestamp { get; }

    public bool IsServoEnabled { get; }

    public HomeState HomeState { get; }

    /// <summary>已映射的当前主故障；RawStatus 仍保留给厂商诊断。</summary>
    public AxisFault? Fault { get; }

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
            && HomeState == other.HomeState
            && EqualityComparer<AxisFault?>.Default.Equals(Fault, other.Fault);
    }
}
