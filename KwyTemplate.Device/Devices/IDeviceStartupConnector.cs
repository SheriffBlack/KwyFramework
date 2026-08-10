namespace KwyTemplate.Device.Devices;

public interface IDeviceStartupConnector : IAsyncDisposable, IDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
}

