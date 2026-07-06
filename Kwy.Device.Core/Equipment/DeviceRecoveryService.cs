using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class DeviceRecoveryService : IDeviceRecoveryService
{
    private readonly DeviceRecoveryParticipant participant;

    public DeviceRecoveryService(IDeviceStateSynchronizer stateSynchronizer, IDeviceSafetyGuard safetyGuard)
    {
        participant = new DeviceRecoveryParticipant("Default", stateSynchronizer, safetyGuard);
    }

    public async Task<DeviceRecoveryResult> RecoverAsync(
        DeviceRecoveryContext context,
        CancellationToken cancellationToken = default)
    {
        return await participant.RecoverAsync(context.Policy, cancellationToken);
    }
}
