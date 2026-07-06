namespace Kwy.Device.Abstractions.Equipment;

public sealed record EquipmentOperationResult(
    bool IsSuccess,
    EquipmentRunState State,
    string? Message = null);

public interface IEquipmentProcessController
{
    Task<EquipmentOperationResult> InitializeAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> StartAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> PauseAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> ResumeAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> StopAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> AbortAsync(CancellationToken cancellationToken = default);

    Task<EquipmentOperationResult> ClearAsync(CancellationToken cancellationToken = default);
}
