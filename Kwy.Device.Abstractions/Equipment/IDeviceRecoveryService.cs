namespace Kwy.Device.Abstractions.Equipment;

public interface IDeviceRecoveryService
{
    Task<DeviceRecoveryResult> RecoverAsync(
        DeviceRecoveryContext context,
        CancellationToken cancellationToken = default);
}
