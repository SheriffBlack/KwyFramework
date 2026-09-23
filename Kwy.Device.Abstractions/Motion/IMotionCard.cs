using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Device.Abstractions.Motion;

/// <summary>物理运动控制卡的设备生命周期入口；工艺层不应直接依赖它。</summary>
public interface IMotionCard : IDevice, IConfigurableDevice
{
}

/// <summary>物理单轴控制能力，仅供设备适配器和 Core 执行器使用。</summary>
public interface IAxisMotionController
{
    void ServoOn(short axis);

    void ServoOff(short axis);

    void ClearError(short axis);

    void MoveJog(short axis, double velocity);

    void Stop(short axis);

    void Abort(short axis);

    void GoHome(short axis);

    void SetSoftLimit(short axis, double positive, double negative);
}

/// <summary>读取厂商原始轴状态的能力；业务诊断应优先使用状态监视器快照。</summary>
public interface IAxisStatusReader
{
    double GetPosition(short axis);

    double GetEncoderPosition(short axis);

    double GetVelocity(short axis);

    int GetStatus(short axis);

    bool IsMoving(short axis);

    bool IsPositiveLimit(short axis);

    bool IsNegativeLimit(short axis);

    bool IsAlarm(short axis);
}

/// <summary>读取一次物理轴状态快照，适用于厂商支持的原子读取。</summary>
public interface IAxisSnapshotReader
{
    MotionAxisSnapshot GetAxisSnapshot(short axis);
}

/// <summary>批量读取多轴快照，供状态监视器降低 SDK 调用次数。</summary>
public interface IBulkAxisSnapshotReader
{
    MotionAxisSnapshot[] GetMultipleAxisSnapshots(short[] axes);
}

/// <summary>由调用方提供缓冲区的批量快照读取，避免后台扫描产生数组分配。</summary>
public interface IBufferedAxisSnapshotReader
{
    void GetMultipleAxisSnapshots(short[] axes, MotionAxisSnapshot[] destination);
}

/// <summary>供 Core 与诊断读取的轴状态来源，可能是缓存快照或直接硬件读取。</summary>
public interface IMotionStateProvider
{
    event Action<MotionAxisSnapshot>? AxisSnapshotCaptured;

    event EventHandler<MotionAxisSnapshotChangedEventArgs>? AxisSnapshotChanged;

    event EventHandler<ErrorOccurredEventArgs>? MonitorErrorOccurred;

    MotionAxisSnapshot GetAxisSnapshot(short axis);

    IReadOnlyDictionary<short, MotionAxisSnapshot> GetAllAxisSnapshots();
}

/// <summary>后台状态监视器；负责刷新、事件发布与故障检测，不负责下发运动命令。</summary>
public interface IMotionStateMonitor : IMotionStateProvider, IDisposable, IAsyncDisposable
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>控制器原生坐标系插补能力；业务应通过 IMotionGroupExecutor 使用业务轴 ID。</summary>
public interface IInterpolationMotionController
{
    void InitCoordinateSystem(short crdIndex, short[] axes);

    void MoveLinear(short crdIndex, double[] positions, double velocity, double acc);

    void MoveArc(short crdIndex, double x, double y, double xCenter, double yCenter, short dir, double velocity, double acc);

    void StartInterpolation(short crdIndex);

    void StopCoordinateSystem(short crdIndex);

    bool IsCrdMoving(short crdIndex);

    Task WaitForCoordinateSystemStoppedAsync(short crdIndex, CancellationToken cancellationToken = default);

    Task WaitForCoordinateSystemCompletedAsync(
        short crdIndex,
        double[] targetPositions,
        double tolerance,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 控制器硬件位置比较输出能力，例如飞拍或点胶触发；参数均为物理卡层单位。
/// 调用只配置控制器比较规则，实际触发由控制器硬件完成，严禁用上位机轮询位置后输出 DO 替代。
/// </summary>
public interface IPositionCompareOutput
{
    void EnablePso(short axis, double[] triggerPositions, double pulseScale = 10000.0, short pulseWidthUs = 20);

    void DisablePso();
}

/// <summary>等待物理卡动作结束的底层能力；超时与到位判定由调用方明确传入。</summary>
public interface IMotionWaiter
{
    Task WaitForAxisStoppedAsync(short axis, CancellationToken cancellationToken = default);

    Task WaitForAxisStoppedAsync(short axis, TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<MotionCompletionResult> WaitForAxisCompletedAsync(
        short axis,
        double targetPosition,
        double tolerance,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task WaitForHomeCompletedAsync(short axis, CancellationToken cancellationToken = default);

    Task<HomeStatus> WaitForHomeCompletedAsync(short axis, TimeSpan timeout, CancellationToken cancellationToken = default);
}
