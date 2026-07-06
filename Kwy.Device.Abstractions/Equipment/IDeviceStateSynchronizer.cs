namespace Kwy.Device.Abstractions.Equipment;

public interface IDeviceStateSynchronizer
{
    Task<DeviceSyncResult> SyncStateAsync(CancellationToken cancellationToken = default);
}
