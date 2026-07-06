namespace Kwy.Device.Abstractions.Equipment;

public sealed record EquipmentRecoveryStepResult(
    string DeviceId,
    DeviceRecoveryResult Result);

public sealed record EquipmentRecoveryOrchestrationResult(
    IReadOnlyList<EquipmentRecoveryStepResult> Steps)
{
    public bool IsRecovered => Steps.All(step => step.Result.IsRecovered);
}

public interface IEquipmentRecoveryOrchestrator
{
    Task<EquipmentRecoveryOrchestrationResult> RecoverAsync(
        RecoveryPolicy policy,
        CancellationToken cancellationToken = default);
}
