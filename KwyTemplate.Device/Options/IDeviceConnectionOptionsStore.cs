namespace KwyTemplate.Device.Options;

public interface IDeviceConnectionOptionsStore
{
    ValueTask<DeviceConnectionOptions> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(DeviceConnectionOptions options, CancellationToken cancellationToken = default);
}
