using Kwy.Device.Abstractions;

namespace KwyTemplate.Flow.DeviceProfiles;

public interface IMachineDeviceResolver
{
    MachineDeviceProfile? CurrentProfile { get; }

    Task ActivateAsync(MachineDeviceProfile profile, CancellationToken cancellationToken = default);

    bool TryGetDevice<TCapability>(string roleOrDeviceId, out TCapability device)
        where TCapability : class;

    TCapability GetRequiredDevice<TCapability>(string roleOrDeviceId)
        where TCapability : class;
}
