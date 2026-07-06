namespace KwyTemplate.Device.Connections;

public interface IDeviceStartupService
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
