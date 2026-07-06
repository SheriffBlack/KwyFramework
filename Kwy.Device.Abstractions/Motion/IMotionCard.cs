using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// Marker and lifecycle interface for motion cards.
/// Advanced capabilities are expressed by separate capability interfaces.
/// </summary>
public interface IMotionCard : IDevice, IConfigurableDevice
{
}

/// <summary>
/// Standard motion card capability set for common single-axis motion scenarios.
/// </summary>
public interface IStandardMotionCard :
    IMotionCard,
    IAxisMotionController,
    IMotionProfileController,
    IAxisStatusReader,
    IAxisSnapshotReader,
    IHomeStatusReader,
    IMotionWaiter
{
}

/// <summary>
/// Advanced motion card capability set for cards that support coordinate interpolation.
/// Optional features such as IO and position compare output remain separate capabilities.
/// </summary>
public interface IAdvancedMotionCard :
    IStandardMotionCard,
    IInterpolationMotionController
{
}

/// <summary>
/// Basic single-axis motion control capability.
/// </summary>
public interface IAxisMotionController
{
    void ServoOn(short axis);

    void ServoOff(short axis);

    void ClearError(short axis);

    void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5);

    void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5);

    void MoveJog(short axis, double velocity);

    void Stop(short axis);

    void Abort(short axis);

    void GoHome(short axis);

    void SetSoftLimit(short axis, double positive, double negative);
}

/// <summary>
/// Axis status and position read capability.
/// </summary>
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

/// <summary>
/// Axis state snapshot read capability.
/// </summary>
public interface IAxisSnapshotReader
{
    MotionAxisSnapshot GetAxisSnapshot(short axis);
}

/// <summary>
/// Bulk axis state snapshot read capability.
/// </summary>
public interface IBulkAxisSnapshotReader
{
    MotionAxisSnapshot[] GetMultipleAxisSnapshots(short[] axes);
}

public interface IBufferedAxisSnapshotReader
{
    void GetMultipleAxisSnapshots(short[] axes, MotionAxisSnapshot[] destination);
}

/// <summary>
/// Cached or live motion state access.
/// </summary>
public interface IMotionStateProvider
{
    event Action<MotionAxisSnapshot>? AxisSnapshotCaptured;

    event EventHandler<MotionAxisSnapshotChangedEventArgs>? AxisSnapshotChanged;

    event EventHandler<ErrorOccurredEventArgs>? MonitorErrorOccurred;

    MotionAxisSnapshot GetAxisSnapshot(short axis);

    IReadOnlyDictionary<short, MotionAxisSnapshot> GetAllAxisSnapshots();
}

/// <summary>
/// Background motion state monitor that keeps axis snapshots up to date.
/// </summary>
public interface IMotionStateMonitor : IMotionStateProvider, IDisposable, IAsyncDisposable
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinate interpolation capability.
/// </summary>
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
/// Optional position synchronized output / compare output capability.
/// </summary>
public interface IPositionCompareOutput
{
    void EnablePso(short axis, double[] triggerPositions, double pulseScale = 10000.0, short pulseWidthUs = 20);

    void DisablePso();
}

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
