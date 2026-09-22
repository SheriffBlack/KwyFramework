namespace Kwy.Device.Abstractions.Motion;

/// <summary>运动动作的统一生命周期状态；用于追溯，不替代厂商实时状态字。</summary>
public enum MotionOperationState
{
    Created,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>可机器读取的动作目标，避免用字符串拼接诊断信息。</summary>
public sealed record MotionOperationTarget(
    MotionRequestKind Kind,
    IReadOnlyDictionary<string, double>? TargetPositions = null,
    string? SensorPointId = null,
    MotionProfile? Profile = null);

/// <summary>可追溯的运动命令生命周期快照。</summary>
public sealed record MotionOperationSnapshot(
    Guid OperationId,
    string ResourceId,
    DateTimeOffset StartedAt,
    MotionOperationState State,
    MotionOperationTarget? Target = null,
    string? CancellationReason = null,
    Exception? Fault = null);

/// <summary>记录单轴、插补与寻边的业务动作生命周期，订阅者不应反向控制设备。</summary>
public interface IMotionOperationTracker
{
    event Action<MotionOperationSnapshot>? OperationChanged;

    IReadOnlyCollection<MotionOperationSnapshot> ActiveOperations { get; }

    MotionOperationSnapshot Start(string resourceId, MotionOperationTarget? target = null);

    void Complete(MotionOperationSnapshot operation, MotionOperationState state, Exception? fault = null, string? cancellationReason = null);
}

/// <summary>单轴与运动组共享的资源独占器；获取失败表示已有动作占用相关轴。</summary>
public interface IMotionResourceLock
{
    ValueTask<IDisposable> AcquireAsync(IEnumerable<string> axisIds, CancellationToken cancellationToken = default);
}