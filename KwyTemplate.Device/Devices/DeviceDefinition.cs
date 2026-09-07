using Kwy.Device.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KwyTemplate.Device.Devices;

public abstract class DeviceDefinition
{
    protected DeviceDefinition(string deviceId, string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        DeviceId = deviceId;
        DeviceName = deviceName;
    }

    public string DeviceId { get; }

    public string DeviceName { get; }

    public abstract IDevice CreateDevice(IServiceProvider services);

    protected static TService GetRequiredService<TService>(IServiceProvider services)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<TService>();
    }
}
