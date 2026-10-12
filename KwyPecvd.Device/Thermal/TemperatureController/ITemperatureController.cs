namespace KwyPecvd.Device.Thermal.TemperatureController;

/// <summary>
/// 温度控制器设备抽象。
/// 提供温度设定、使能控制和实际温度反馈。
/// </summary>
public interface ITemperatureController : IHardwareComponent
{
    TemperatureControllerDefinition Definition { get; }

    TemperatureControllerSnapshot Snapshot { get; }

    event EventHandler< TemperatureControllerSnapshot>? SnapshotChanged;

    Task<CommandResult> SetTargetTemperatureAsync(
        double target,
        CancellationToken cancellationToken = default);

    Task<CommandResult> EnableAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> DisableAsync(
        CancellationToken cancellationToken = default);
}