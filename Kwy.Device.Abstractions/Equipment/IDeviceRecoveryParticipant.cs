namespace Kwy.Device.Abstractions.Equipment;

public interface IDeviceRecoveryParticipant
{
    string DeviceId { get; }

    Task<DeviceRecoveryResult> RecoverAsync(
        RecoveryPolicy policy,
        CancellationToken cancellationToken = default);
}
