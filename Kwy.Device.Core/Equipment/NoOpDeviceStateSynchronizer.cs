using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class NoOpDeviceStateSynchronizer : IDeviceStateSynchronizer
{
    public Task<DeviceSyncResult> SyncStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DeviceSyncResult(
            DeviceSyncState.Unknown,
            Array.Empty<DeviceSyncItem>(),
            "No device state synchronizer is configured."));
    }
}
