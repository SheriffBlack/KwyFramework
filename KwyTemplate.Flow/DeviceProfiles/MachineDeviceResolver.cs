using Kwy.Device.Abstractions;
using KwyTemplate.Device.Connections;

namespace KwyTemplate.Flow.DeviceProfiles;

public sealed class MachineDeviceResolver : IMachineDeviceResolver
{
    private readonly IDeviceConnectionService connectionService;
    private readonly IDeviceRegistry deviceRegistry;
    private MachineDeviceProfile? currentProfile;

    public MachineDeviceResolver(
        IDeviceConnectionService connectionService,
        IDeviceRegistry deviceRegistry)
    {
        this.connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        this.deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
    }

    public MachineDeviceProfile? CurrentProfile => currentProfile;

    public async Task ActivateAsync(MachineDeviceProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile.Validate();
        await connectionService.ConnectDevicesAsync(profile.GetRequiredDeviceIds(), cancellationToken).ConfigureAwait(false);
        currentProfile = profile;
    }

    public bool TryGetDevice<TCapability>(string roleOrDeviceId, out TCapability device)
        where TCapability : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleOrDeviceId);

        string deviceId = ResolveDeviceId(roleOrDeviceId);
        try
        {
            return deviceRegistry.TryGetDevice(deviceId, out device);
        }
        catch (ObjectDisposedException)
        {
            device = null!;
            return false;
        }
    }

    public TCapability GetRequiredDevice<TCapability>(string roleOrDeviceId)
        where TCapability : class
    {
        if (TryGetDevice(roleOrDeviceId, out TCapability device))
        {
            return device;
        }

        throw new KeyNotFoundException($"Device '{roleOrDeviceId}' is not available.");
    }

    private string ResolveDeviceId(string roleOrDeviceId)
    {
        MachineDeviceRequirement? requirement = currentProfile?.Devices.FirstOrDefault(device =>
            string.Equals(device.Role, roleOrDeviceId, StringComparison.OrdinalIgnoreCase));

        return requirement?.DeviceId ?? roleOrDeviceId;
    }
}
