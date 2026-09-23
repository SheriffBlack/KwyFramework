namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 运动控制器经确认可用的原生能力。
/// 厂商适配器只声明其型号、固件与 SDK 实际支持的能力，不能用上位机定时循环伪造实时能力。
/// </summary>
public sealed record MotionControllerCapabilities
{
    public bool SupportsNativeContinuousContour { get; init; }
    public bool SupportsNativeLookAhead { get; init; }
    public bool SupportsNativeJerkLimiting { get; init; }
    public bool SupportsNativeSpline { get; init; }
    public bool SupportsNativeElectronicGear { get; init; }
    public bool SupportsNativeElectronicCam { get; init; }
    public bool SupportsControllerVirtualAxis { get; init; }
    public bool SupportsHardwarePositionCompareOutput { get; init; }
}

/// <summary>由具体运动控制卡公开其已确认的原生运动能力。</summary>
public interface IMotionControllerCapabilities
{
    MotionControllerCapabilities Capabilities { get; }
}

/// <summary>连续轮廓程序编译时必须满足的控制器原生能力。</summary>
public sealed record ControllerMotionProgramRequirements
{
    public bool RequireNativeContinuousContour { get; init; } = true;
    public bool RequireNativeLookAhead { get; init; }
    public bool RequireNativeJerkLimiting { get; init; }
    public bool RequireNativeSpline { get; init; }

    /// <summary>能力不足时由厂商编译器拒绝，禁止悄悄退化为 Windows 定时点流。</summary>
    public void ValidateAgainst(MotionControllerCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        if ((RequireNativeContinuousContour && !capabilities.SupportsNativeContinuousContour)
            || (RequireNativeLookAhead && !capabilities.SupportsNativeLookAhead)
            || (RequireNativeJerkLimiting && !capabilities.SupportsNativeJerkLimiting)
            || (RequireNativeSpline && !capabilities.SupportsNativeSpline))
        {
            throw new NotSupportedException("The motion controller does not provide all required native contour capabilities.");
        }
    }
}

/// <summary>
/// 将业务运动程序交给厂商适配器编译的输入。
/// 其中预检关节轨迹仅用于编译前的可达性、安全和预计时间检查，不是通用控制器点流格式。
/// </summary>
public sealed record ControllerMotionProgramRequest(
    string DeviceId,
    string MotionGroupId,
    MotionProgram Program,
    long CoordinateFrameVersion,
    ControllerMotionProgramRequirements Requirements,
    JointTrajectory? PreflightTrajectory = null)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(MotionGroupId);
        ArgumentNullException.ThrowIfNull(Program);
        ArgumentNullException.ThrowIfNull(Requirements);
        Program.Validate();
        if (CoordinateFrameVersion < 0) throw new ArgumentOutOfRangeException(nameof(CoordinateFrameVersion));
        if (PreflightTrajectory is not null
            && (!string.Equals(PreflightTrajectory.MechanismId, Program.MechanismId, StringComparison.OrdinalIgnoreCase)
                || PreflightTrajectory.CoordinateFrameVersion != CoordinateFrameVersion))
        {
            throw new ArgumentException("The preflight trajectory must match the program mechanism and coordinate-frame version.", nameof(PreflightTrajectory));
        }
    }
}

/// <summary>
/// 厂商编译后的不透明原生运动程序，例如控制器坐标系、缓冲程序或控制器脚本。
/// Core 与业务层只保留身份和版本信息，绝不依赖厂商 SDK 的点表、句柄或缓冲区类型。
/// </summary>
public interface IControllerMotionProgram
{
    string Id { get; }
    string DeviceId { get; }
    string MotionGroupId { get; }
    long CoordinateFrameVersion { get; }
}

/// <summary>
/// 一个具体控制器型号的原生程序适配器。
/// 编译与执行必须由同一厂商适配器承担，避免 Core、工艺层或另一张卡错误解释厂商私有的程序句柄。
/// </summary>
public interface IControllerMotionProgramAdapter : IMotionControllerCapabilities
{
    /// <summary>将语义 MotionProgram 编译为控制器实时内核可执行的原生程序。</summary>
    Task<IControllerMotionProgram> CompileAsync(
        ControllerMotionProgramRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>任务完成表示控制器原生程序已结束；不得只表示命令已写入控制器缓冲区。</summary>
    Task ExecuteAsync(IControllerMotionProgram program, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工艺层执行连续轮廓程序的唯一入口。
/// Core 负责业务轴、坐标系、可达性和安全预检；控制器适配器负责原生程序编译和实时执行。
/// </summary>
public interface IControllerMotionProgramService
{
    Task ExecuteAsync(
        MotionProgram program,
        ControllerMotionProgramRequirements? requirements = null,
        CancellationToken cancellationToken = default);
}
