namespace Kwy.Device.Abstractions.Motion;

/// <summary>物理控制卡上的轴地址；业务稳定标识必须使用 AxisDefinition.Id，而不是此地址。</summary>
public readonly record struct AxisAddress(string DeviceId, short Channel)
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

/// <summary>轴在工程单位下的软限位及动态上限；用于保护机构，不能由单次工艺参数突破。</summary>
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

/// <summary>轴级默认到位规则；工艺动作可以更严格，但不应散落地重复这些默认值。</summary>
public sealed record AxisMotionDefaults
{
    public double PositionTolerance { get; init; } = 0.01;
    public TimeSpan MotionTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SettlingTime { get; init; } = TimeSpan.Zero;
    public double? SettlingVelocityThreshold { get; init; }

    public void Validate()
    {
        if (!double.IsFinite(PositionTolerance) || PositionTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(PositionTolerance));
        if (MotionTimeout <= TimeSpan.Zero && MotionTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(MotionTimeout));
        if (SettlingTime < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(SettlingTime));
        ValidateOptionalNonNegative(SettlingVelocityThreshold, nameof(SettlingVelocityThreshold));
    }

    public MotionExecutionOptions ToExecutionOptions() => new()
    {
        PositionTolerance = PositionTolerance,
        Timeout = MotionTimeout,
        SettlingTime = SettlingTime,
        SettlingVelocityThreshold = SettlingVelocityThreshold
    };

    private static void ValidateOptionalNonNegative(double? value, string name)
    {
        if (value is { } number && (!double.IsFinite(number) || number < 0))
            throw new ArgumentOutOfRangeException(name, number, $"{name} must be finite and non-negative.");
    }
}

/// <summary>物理轴回零配方；Mode 的具体含义由厂商适配器映射，业务流程不解析它。</summary>
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

/// <summary>闭环反馈来源，用于诊断和双闭环配置，不等同于厂商状态字。</summary>
public enum AxisFeedbackSource
{
    /// <summary>电机自带编码器（电机端）</summary>
    MotorEncoder,

    /// <summary>负载端编码器/旋转编码器 </summary>
    LoadEncoder,

    /// <summary>光栅尺（直线负载端，线性标尺）</summary>
    LinearScale
}

/// <summary>单轴不可进入的机械位置区间，例如夹具、线缆或维护禁区。</summary>
public readonly record struct AxisForbiddenRange(double Minimum, double Maximum)
{
    public bool Contains(double position) => position >= Minimum && position <= Maximum;

    public void Validate()
    {
        if (!double.IsFinite(Minimum) || !double.IsFinite(Maximum) || Minimum >= Maximum)
            throw new ArgumentException("Forbidden range minimum must be less than maximum.");
    }
}

/// <summary>垂直轴失能时抱闸与伺服的安全顺序。</summary>
public enum AxisBrakeDisableStrategy
{
    EngageBeforeServoOff,
    ServoOffBeforeEngage
}

/// <summary>轴级安全策略；跨轴空间关系应配置在运动组禁入区，而非塞入单轴。</summary>
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

/// <summary>旋转轴位置表达方式：累计角度或模周期角度。</summary>
public enum RotaryPositionMode
{
    /// <summary>累计角度，正转+，反转-</summary>
    Accumulated,

    /// <summary>模周期角度，[0~360°] 循环</summary>
    Modulo
}

/// <summary>旋转轴专属路径约束，例如缠绕限制、偏好方向和禁入角度。</summary>
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

/// <summary>机械/标定坐标修正与反馈配置；标定算法由独立坐标转换服务承载。</summary>
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

/// <summary>轴可参与的运动关系。同步主从能力必须由设备配置显式声明，默认不允许隐式耦合。</summary>
[Flags]
public enum AxisMotionCapability
{
    SingleAxis = 1,
    Interpolation = 2,
    SynchronizationMaster = 4,
    SynchronizationFollower = 8
}

/// <summary>
/// 物理轴与虚拟轴共享的业务定义。运动关系始终引用 <see cref="Id"/>，
/// 不在插补、凸轮或齿轮模型中重复维护轴的工程属性。
/// </summary>
public abstract record AxisResourceDefinition
{
    /// <summary>业务稳定 ID，例如 transport.x；配方、运动组、凸轮和齿轮均以此引用。</summary>
    public required string Id { get; init; }
    /// <summary>供 HMI、报警和维护人员显示的名称，可改名而不影响业务绑定。</summary>
    public required string DisplayName { get; init; }
    /// <summary>工程单位、方向和脉冲换算规则。</summary>
    public required AxisEngineeringConfig Engineering { get; init; }
    /// <summary>机械行程与动态能力上限。</summary>
    public AxisLimitConfig Limits { get; init; } = new();
    /// <summary>该轴的一般定位到位规则。</summary>
    public AxisMotionDefaults Defaults { get; init; } = new();
    /// <summary>允许参与的运动关系；插补或同步前均会在配置校验阶段检查。</summary>
    public AxisMotionCapability Capabilities { get; init; } = AxisMotionCapability.SingleAxis | AxisMotionCapability.Interpolation;

    protected void ValidateCommon()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentNullException.ThrowIfNull(Engineering);
        ArgumentNullException.ThrowIfNull(Limits);
        ArgumentNullException.ThrowIfNull(Defaults);
        if (Capabilities == 0 || (Capabilities & ~AllCapabilities) != 0)
            throw new ArgumentOutOfRangeException(nameof(Capabilities));
        Engineering.Validate();
        Limits.Validate();
        Defaults.Validate();
    }

    private const AxisMotionCapability AllCapabilities =
        AxisMotionCapability.SingleAxis |
        AxisMotionCapability.Interpolation |
        AxisMotionCapability.SynchronizationMaster |
        AxisMotionCapability.SynchronizationFollower;
}

/// <summary>
/// 物理轴的完整应用层定义。
/// 厂商特定的电气和控制器参数保留在运动控制卡的实现中。
/// </summary>
public sealed record AxisDefinition : AxisResourceDefinition
{
    /// <summary>承载该轴的物理运动控制卡设备 ID。</summary>
    public required string DeviceId { get; init; }
    /// <summary>该控制卡内部的物理轴通道号。</summary>
    public short Channel { get; init; }
    /// <summary>回零配方。</summary>
    public AxisHomeDefinition Home { get; init; } = new();
    /// <summary>回零、抱闸、互锁与禁入区等安全策略。</summary>
    public AxisSafetyDefinition Safety { get; init; } = new();
    public RotaryAxisDefinition? Rotary { get; init; }
    public AxisCalibrationConfig Calibration { get; init; } = new();

    public AxisAddress PhysicalId => new(DeviceId, Channel);

    public void Validate()
    {
        ValidateCommon();
        ArgumentNullException.ThrowIfNull(Home);
        ArgumentNullException.ThrowIfNull(Safety);
        ArgumentNullException.ThrowIfNull(Calibration);
        PhysicalId.Validate();
        Home.Validate();
        Safety.Validate();
        Calibration.Validate();
        Rotary?.Validate();
        if (Rotary is not null && Engineering.Unit != MotionUnit.Degree)
            throw new InvalidOperationException("Rotary axis settings require MotionUnit.Degree.");
    }
}
