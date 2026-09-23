namespace Kwy.Device.Abstractions.Motion;

/// <summary>业务坐标使用的工程单位；控制卡脉冲换算由 <see cref="AxisEngineeringConfig"/> 统一处理。</summary>
public enum MotionUnit
{
    /// <summary>脉冲单位（底层原始计数单位）</summary>
    Pulse,

    /// <summary>毫米，直线轴工程单位</summary>
    Millimeter,

    /// <summary>角度，旋转轴工程单位</summary>
    Degree
}

/// <summary>工程坐标与控制卡脉冲之间的换算配置，不承载限位、回零等机械工艺规则。</summary>
public sealed record AxisEngineeringConfig
{
    /// <summary>业务位置的单位，例如搬运轴为毫米、旋转轴为角度。</summary>
    public MotionUnit Unit { get; init; } = MotionUnit.Pulse;

    /// <summary>每个工程单位对应的控制卡脉冲数。</summary>
    public double PulsesPerUnit { get; init; } = 1;

    /// <summary>业务坐标正方向与电机脉冲正方向是否相反。</summary>
    public bool DirectionReversed { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(Unit))
            throw new ArgumentOutOfRangeException(nameof(Unit));
        if (!double.IsFinite(PulsesPerUnit) || PulsesPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PulsesPerUnit), PulsesPerUnit, "PulsesPerUnit must be finite and greater than 0.");
        }
    }
}

/// <summary>一次运动的速度曲线约束；业务传工程单位，设备层负责转换并限制在轴能力内。</summary>
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

/// <summary>控制器报告的本次回零状态；其可信度应同时参考 <see cref="AxisHomeValidity"/>。</summary>
public enum HomeState
{
    /// <summary>未知：尚未发起回零，无本次回零动作信息</summary>
    Unknown,

    /// <summary>空闲：当前没有在执行回零</summary>
    Idle,

    /// <summary>运行中：正在执行回零流程</summary>
    Running,

    /// <summary>本次回零动作执行成功</summary>
    Succeeded,

    /// <summary>本次回零动作执行失败</summary>
    Failed
}

/// <summary>物理轴回零执行结果，RawStatus 仅用于诊断，不应被业务流程解析。</summary>
public readonly record struct HomeStatus(
    short Axis,
    HomeState State,
    int RawStatus,
    string? ErrorMessage = null)
{
    public bool IsCompleted => State is HomeState.Succeeded or HomeState.Failed;
}

/// <summary>单轴点位动作完成时的实际位置与到位公差。</summary>
public readonly record struct MotionCompletionResult(
    short Axis,
    double TargetPosition,
    double ActualPosition,
    double Tolerance)
{
    public double PositionError => ActualPosition - TargetPosition;
}

/// <summary>单次定位的覆盖参数；未传入时应采用 <see cref="AxisMotionDefaults"/>。</summary>
public sealed class MotionExecutionOptions
{
    /// <summary>实际位置与目标位置允许的最大偏差。</summary>
    public double PositionTolerance { get; set; } = 0.01;

    /// <summary>从命令发出到完成的最长时间。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>调用方取消后是否执行受控停止。</summary>
    public bool StopOnCancellation { get; set; } = true;

    public TimeSpan StartDetectionDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan SettlingTime { get; set; } = TimeSpan.Zero;

    public double? SettlingVelocityThreshold { get; set; }

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

        if (SettlingTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SettlingTime));
        ValidateOptionalNonNegative(SettlingVelocityThreshold, nameof(SettlingVelocityThreshold));
    }

    private static void ValidateOptionalNonNegative(double? value, string name)
    {
        if (value is { } number && (!double.IsFinite(number) || number < 0))
            throw new ArgumentOutOfRangeException(name, number, $"{name} must be finite and non-negative.");
    }
}

/// <summary>等待位置穿越时的有效方向，避免反向经过误触发工艺动作。</summary>
public enum PositionCrossingDirection
{
    /// <summary>正向穿越：位置由小变大穿过阈值</summary>
    Positive,

    /// <summary>负向穿越：位置由大变小穿过阈值</summary>
    Negative
}

/// <summary>寻边触发后的停轴责任方。</summary>
public enum SensorStopMode
{
    /// <summary>控制器硬件停轴</summary>
    ControllerHardwareStop,

    /// <summary>软件停轴</summary>
    SoftwareStop
}

/// <summary>寻边动作配置；传感器由逻辑 IO 点位提供，不在此模型保存物理通道。</summary>
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

/// <summary>寻边完成时记录触发的逻辑点位与实际位置，供工艺追溯使用。</summary>
public readonly record struct SensorSeekResult(
    short Axis,
    string SensorPointId,
    double Position,
    SensorStopMode StopMode);

/// <summary>供安全守卫和操作追溯识别的运动请求类别。</summary>
public enum MotionRequestKind
{
    /// <summary>绝对定位运动</summary>
    Absolute,

    /// <summary>相对定位运动</summary>
    Relative,

    /// <summary>点动Jog运动</summary>
    Jog,

    /// <summary>回零运动</summary>
    Home
}

/// <summary>物理卡层安全检查使用的请求；Axis 为卡内通道，不是业务轴 ID。</summary>
public readonly record struct MotionRequest(
    short Axis,
    MotionRequestKind Kind,
    double? TargetPosition = null,
    int Direction = 0,
    bool RequiresHomed = true);

/// <summary>动作发出前的准入拒绝原因，供报警、人机界面与日志统一显示；不是控制器实时安全故障。</summary>
public sealed record MotionAdmissionViolation(string Code, string Message);

/// <summary>动作准入检查结果。</summary>
public sealed record MotionAdmissionResult(IReadOnlyList<MotionAdmissionViolation> Violations)
{
    public bool IsAllowed => Violations.Count == 0;

    public static MotionAdmissionResult Allowed { get; } = new(Array.Empty<MotionAdmissionViolation>());
}

/// <summary>配方或设备维护保存的命名位置集合，始终以业务轴 ID 绑定。</summary>
public sealed record NamedPositionSet(
    string Name,
    /// <summary>以 AxisDefinition.Id 为键的目标位置，禁止保存物理轴通道号。</summary>
    IReadOnlyDictionary<string, double> Positions,
    string? ProfileName = null);
