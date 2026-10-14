namespace KwyPecvd.Device;

/// <summary>
/// 表示支持显式复位的硬件组件能力。
/// </summary>
public interface IResettableHardwareComponent
{
    Task<CommandResult> ResetAsync(
        CancellationToken cancellationToken = default);
}
