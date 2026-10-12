namespace KwyPecvd.Device.RfMatcher;

/// <summary>
/// RF匹配器设备抽象。
/// </summary>
public interface IRfMatcher : IHardwareComponent
{
    RfMatcherDefinition Definition { get; }

    RfMatcherSnapshot Snapshot { get; }

    event EventHandler<RfMatcherSnapshot>? SnapshotChanged;

    /// <summary>
    /// 设置自动或手动模式。
    /// </summary>
    Task<CommandResult> SetModeAsync(
        RfMatcherMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在手动模式下设置Tune和Load位置。
    /// </summary>
    Task<CommandResult> SetManualPositionAsync(
        double tunePosition,
        double loadPosition,
        CancellationToken cancellationToken = default);
}