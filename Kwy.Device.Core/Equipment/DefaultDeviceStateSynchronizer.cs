using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class DefaultDeviceStateSynchronizer : IDeviceStateSynchronizer
{
    private readonly IDevice device;

    public DefaultDeviceStateSynchronizer(IDevice device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public Task<DeviceSyncResult> SyncStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new[]
        {
            new DeviceSyncItem(nameof(device.DeviceId), device.DeviceId),
            new DeviceSyncItem(nameof(device.DeviceName), device.DeviceName),
            new DeviceSyncItem(nameof(device.State), device.State.ToString()),
            new DeviceSyncItem(nameof(device.IsConnected), device.IsConnected.ToString())
        };

        DeviceSyncResult result = device.IsConnected
            ? DeviceSyncResult.Synchronized(items)
            : new DeviceSyncResult(DeviceSyncState.Offline, items, "Device is not connected.");

        return Task.FromResult(result);
    }
}
