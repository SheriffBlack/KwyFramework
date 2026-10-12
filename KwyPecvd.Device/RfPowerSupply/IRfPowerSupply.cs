namespace KwyPecvd.Device.RfPowerSupply;

/// <summary>
/// RF电源设备抽象。
/// 提供功率设定、输出启停和正反向功率反馈。
/// </summary>
public interface IRfPowerSupply : IHardwareComponent
{
    RfPowerSupplyDefinition Definition { get; }

    RfPowerSupplySnapshot Snapshot { get; }

    event EventHandler<RfPowerSupplySnapshot>? SnapshotChanged;

    Task<CommandResult> SetPowerAsync(
        double power,
        CancellationToken cancellationToken = default);

    Task<CommandResult> EnableAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> DisableAsync(
        CancellationToken cancellationToken = default);
}