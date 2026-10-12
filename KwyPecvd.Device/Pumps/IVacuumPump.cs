namespace KwyPecvd.Device.Pumps;

/// <summary>
/// 真空泵设备抽象。
/// 提供启停命令、运行反馈及故障状态。
/// </summary>
public interface IVacuumPump : IHardwareComponent
{
    VacuumPumpDefinition Definition { get; }

    VacuumPumpSnapshot Snapshot { get; }

    event EventHandler<VacuumPumpSnapshot>? SnapshotChanged;

    Task<CommandResult> StartAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> StopAsync(
        CancellationToken cancellationToken = default);
}