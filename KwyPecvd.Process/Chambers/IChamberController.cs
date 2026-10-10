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

    Task<CommandResult> StopGasFlowAsync(
        string gasName,
        CancellationToken cancellationToken = default);

    Task<CommandResult> StartGasPreparationAsync(
        IReadOnlyCollection<ChamberGasTarget> targets,
        CancellationToken cancellationToken = default);
}