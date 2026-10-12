namespace KwyPecvd.Device.OnOffValve;

/// <summary>
/// 开关阀设备抽象。
/// 提供开关命令、位置反馈及动作状态。
/// </summary>
public interface IOnOffValve : IHardwareComponent
{
    OnOffValveDefinition Definition { get; }

    OnOffValveSnapshot Snapshot { get; }

    event EventHandler<OnOffValveSnapshot>? SnapshotChanged;

    Task<CommandResult> OpenAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> CloseAsync(
        CancellationToken cancellationToken = default);
}
