using KwyPecvd.Device;
using KwyPecvd.Process.Equipment;

namespace KwyPecvd.Process.Chambers;

/// <summary>
/// PM 腔室的控制器
/// </summary>
public interface IChamberController : IEquipmentModuleController, ICyclicEquipmentModuleController
{
    ChamberDefinition Definition { get; }

    ChamberSnapshot Snapshot { get; }

    Task<CommandResult> InitializeAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> SetGasFlowAsync(
        string gasName,
        double target,
        TimeSpan rampDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止单路气体，偏调试或底层控制
    /// </summary>
    /// <returns></returns>
    Task<CommandResult> StopGasFlowAsync(
        string gasName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止整个腔体工艺，是正式业务操作。
    /// 不应等待流量真正归零，否则一次命令可能阻塞数秒。
    /// </summary>
    Task<CommandResult> StopProcessAsync(
        CancellationToken cancellationToken = default);

    Task<CommandResult> StartGasPreparationAsync(
        IReadOnlyCollection<ChamberGasTarget> targets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认故障原因已经消失，清除锁存故障。
    /// </summary>
    Task<CommandResult> ResetFaultAsync(
        CancellationToken cancellationToken = default);
}