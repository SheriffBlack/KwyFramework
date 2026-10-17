namespace KwyPecvd.Device.ThrottleValve;

/// <summary>
/// 可连续调节、可执行压力闭环的节流阀。
/// </summary>
public interface IThrottleValve : IHardwareComponent
{
    ThrottleValveDefinition Definition { get; }

    ThrottleValveSnapshot Snapshot { get; }

    event EventHandler<ThrottleValveSnapshot>? SnapshotChanged;

    Task<CommandResult> CloseAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> OpenAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> SetPositionAsync(
        double position,
        CancellationToken cancellationToken = default);

    Task<CommandResult> SetPressureAsync(
        double pressure,
        CancellationToken cancellationToken = default);
}