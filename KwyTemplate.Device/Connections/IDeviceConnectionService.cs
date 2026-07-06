namespace KwyTemplate.Device.Connections;

public interface IDeviceConnectionService
{
    Task ConnectStartupDevicesAsync(CancellationToken cancellationToken = default);

    Task ConnectAllAsync(CancellationToken cancellationToken = default);

    Task DisconnectAllAsync(CancellationToken cancellationToken = default);

    Task ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    Task ConnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default);

    Task DisconnectDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default);
}
