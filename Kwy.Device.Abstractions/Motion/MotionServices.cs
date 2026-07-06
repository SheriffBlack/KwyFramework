using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.Abstractions.Motion;

public interface IMotionProfileController
{
    void MoveAbs(short axis, double position, MotionProfile profile);

    void MoveRel(short axis, double distance, MotionProfile profile);
}

public interface IAxisEngineeringUnitProvider
{
    AxisEngineeringConfig GetAxisEngineeringConfig(short axis);
}

public interface IHomeStatusReader
{
    HomeStatus GetHomeStatus(short axis);
}

public interface IMotionSafetyGuard
{
    MotionSafetyResult Validate(MotionRequest request);

    void ValidateAndThrow(MotionRequest request);
}

/// <summary>
/// Owns the state monitor and executor that belong to one physical motion card.
/// </summary>
public interface IMotionDeviceRuntime : IDisposable, IAsyncDisposable
{
    string DeviceId { get; }

    IMotionCard Card { get; }

    IMotionStateMonitor StateMonitor { get; }

    IAxisMotionExecutor AxisExecutor { get; }
}

/// <summary>
/// Resolves card-specific motion services without relying on ambiguous unkeyed DI registrations.
/// </summary>
public interface IMotionRuntimeRegistry
{
    IReadOnlyCollection<IMotionDeviceRuntime> Runtimes { get; }

    IMotionDeviceRuntime GetRequired(string deviceId);

    IMotionDeviceRuntime GetRequiredSingle();
}

public interface ISafeAxisMotionController : IAxisMotionController, IMotionProfileController
{
}

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
}

public interface INamedPositionRepository
{
    Task<NamedPositionSet?> GetAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NamedPositionSet>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(NamedPositionSet position, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}

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
