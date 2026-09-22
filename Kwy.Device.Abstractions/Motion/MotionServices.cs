using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.Abstractions.Motion;

/// <summary>物理卡的带速度曲线定位能力；位置和轴号均为卡层值。</summary>
public interface IMotionProfileController
{
    void MoveAbs(short axis, double position, MotionProfile profile);

    void MoveRel(short axis, double distance, MotionProfile profile);
}

/// <summary>按物理轴通道获取工程单位换算配置。</summary>
public interface IAxisEngineeringUnitProvider
{
    AxisEngineeringConfig GetAxisEngineeringConfig(short axis);
}

/// <summary>物理卡提供的业务轴定义，用于将稳定业务轴 ID 解析到实际通道。</summary>
public interface IAxisDefinitionProvider : IAxisEngineeringUnitProvider
{
    /// <summary>该物理卡承载的全部业务轴定义，供业务 ID 解析使用。</summary>
    IReadOnlyCollection<AxisDefinition> Axes { get; }

    AxisDefinition GetAxisDefinition(short axis);

    AxisEngineeringConfig IAxisEngineeringUnitProvider.GetAxisEngineeringConfig(short axis)
        => GetAxisDefinition(axis).Engineering;
}

/// <summary>读取控制器当前回零状态，不代表该结果一定仍被自动模式信任。</summary>
public interface IHomeStatusReader
{
    HomeStatus GetHomeStatus(short axis);
}

/// <summary>物理轴运动前的统一安全校验，集中检查限位、回零、互锁与禁入区。</summary>
public interface IMotionSafetyGuard
{
    MotionSafetyResult Validate(MotionRequest request);

    void ValidateAndThrow(MotionRequest request);
}

/// <summary>一张物理运动卡对应的运行时集合，防止不同卡的服务被错误混用。</summary>
public interface IMotionDeviceRuntime : IDisposable, IAsyncDisposable
{
    string DeviceId { get; }

    IMotionCard Card { get; }

    IMotionStateMonitor StateMonitor { get; }

    IAxisMotionExecutor AxisExecutor { get; }
}

/// <summary>按设备 ID 解析物理运动卡运行时，避免多卡场景使用不明确的非键控依赖。</summary>
public interface IMotionRuntimeRegistry
{
    IReadOnlyCollection<IMotionDeviceRuntime> Runtimes { get; }

    IMotionDeviceRuntime GetRequired(string deviceId);

    IMotionDeviceRuntime GetRequiredSingle();
}

/// <summary>已接入统一安全守卫的物理单轴控制器；工艺层仍应优先使用业务执行器。</summary>
public interface ISafeAxisMotionController : IAxisMotionController, IMotionProfileController
{
}

/// <summary>物理轴执行器，统一等待、超时、停止与故障判断；short axis 仅限 Core/适配器。</summary>
public interface IAxisMotionExecutor
{
    Task<MotionCompletionResult> MoveAbsAsync(
        short axis,
        double position,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MotionCompletionResult> MoveRelAsync(
        short axis,
        double distance,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MotionAxisSnapshot> WaitForPositionCrossedAsync(
        short axis,
        double position,
        PositionCrossingDirection direction,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<SensorSeekResult> SeekSensorAsync(
        short axis,
        IIoCardDevice ioDevice,
        int channel,
        double velocity,
        SensorSeekOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SensorSeekResult> SeekSensorAsync(
        short axis,
        string sensorPointId,
        Func<bool> readSensorState,
        double velocity,
        SensorSeekOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 面向工艺与配方的业务轴执行器。只接收 AxisDefinition.Id，
/// 不向调用方暴露物理 DeviceId、卡号或 short 轴通道。
/// </summary>
public interface IBusinessAxisMotionExecutor
{
    Task<MotionCompletionResult> MoveAbsAsync(
        string axisId,
        double position,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MotionCompletionResult> MoveRelAsync(
        string axisId,
        double distance,
        MotionProfile profile,
        MotionExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>以业务轴 ID 和逻辑传感器点位执行寻边；不暴露物理 IO 卡和通道号。</summary>
    Task<SensorSeekResult> SeekSensorAsync(
        string axisId,
        string sensorPointId,
        double velocity,
        SensorSeekOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<HomeStatus> HomeAsync(string axisId, CancellationToken cancellationToken = default);
}

/// <summary>面向工艺的多轴插补执行器；业务不得传坐标系号或物理轴通道。</summary>
public interface IMotionGroupExecutor
{
    Task MoveLinearAsync(LinearMoveCommand command, CancellationToken cancellationToken = default);

    Task MoveArcAsync(ArcMoveCommand command, CancellationToken cancellationToken = default);
}

/// <summary>提供设备已配置的业务运动组定义。</summary>
public interface IMotionGroupDefinitionProvider
{
    IReadOnlyCollection<MotionGroupDefinition> MotionGroups { get; }

    MotionGroupDefinition GetMotionGroup(string groupId);
}

/// <summary>命名位置的持久化边界，例如维护位、换料位或工艺配方位置。</summary>
public interface INamedPositionRepository
{
    Task<NamedPositionSet?> GetAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NamedPositionSet>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(NamedPositionSet position, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>按名称执行一组业务轴定位，调用方不接触物理轴号。</summary>
public interface INamedPositionMotionService
{
    Task MoveToAsync(string name, MotionProfile profile, TimeSpan timeout, CancellationToken cancellationToken = default);
}

public sealed class MotionSafetyException : InvalidOperationException
{
    public MotionSafetyException(IReadOnlyList<MotionSafetyViolation> violations)
        : base(string.Join("; ", violations.Select(item => item.Message)))
    {
        Violations = violations;
    }

    public IReadOnlyList<MotionSafetyViolation> Violations { get; }
}

public sealed class MotionHomeException : InvalidOperationException
{
    public MotionHomeException(HomeStatus status)
        : base(status.ErrorMessage ?? $"Axis {status.Axis} homing failed. RawStatus={status.RawStatus}.")
    {
        Status = status;
    }

    public HomeStatus Status { get; }
}

public abstract class MotionCompletionException : InvalidOperationException
{
    protected MotionCompletionException(short axis, string message)
        : base(message)
    {
        Axis = axis;
    }

    public short Axis { get; }
}

public sealed class MotionAlarmException : MotionCompletionException
{
    public MotionAlarmException(short axis)
        : base(axis, $"Axis {axis} entered alarm state before motion completed.")
    {
    }
}

public sealed class MotionLimitException : MotionCompletionException
{
    public MotionLimitException(short axis, bool positive)
        : base(axis, $"Axis {axis} reached the {(positive ? "positive" : "negative")} limit before motion completed.")
    {
        IsPositiveLimit = positive;
    }

    public bool IsPositiveLimit { get; }
}

public sealed class MotionPositionException : MotionCompletionException
{
    public MotionPositionException(short axis, double targetPosition, double actualPosition, double tolerance)
        : base(axis, $"Axis {axis} stopped before reaching target {targetPosition}. Actual={actualPosition}, tolerance={tolerance}.")
    {
        TargetPosition = targetPosition;
        ActualPosition = actualPosition;
        Tolerance = tolerance;
    }

    public double TargetPosition { get; }

    public double ActualPosition { get; }

    public double Tolerance { get; }
}

public sealed class MotionFollowingErrorException : MotionCompletionException
{
    public MotionFollowingErrorException(short axis, double plannedPosition, double encoderPosition, double limit)
        : base(axis, $"Axis {axis} following error {Math.Abs(plannedPosition - encoderPosition)} exceeds limit {limit}.")
    {
        PlannedPosition = plannedPosition;
        EncoderPosition = encoderPosition;
        Limit = limit;
    }

    public double PlannedPosition { get; }
    public double EncoderPosition { get; }
    public double Limit { get; }
}

public sealed class MotionServoDisabledException : MotionCompletionException
{
    public MotionServoDisabledException(short axis)
        : base(axis, $"Axis {axis} servo was disabled before motion completed.")
    {
    }
}

public sealed class MotionOperationInProgressException : InvalidOperationException
{
    public MotionOperationInProgressException(short axis)
        : base($"Axis {axis} already has an active motion operation.")
    {
        Axis = axis;
    }

    public short Axis { get; }
}