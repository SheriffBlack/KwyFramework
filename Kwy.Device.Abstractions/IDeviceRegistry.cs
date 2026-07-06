namespace Kwy.Device.Abstractions;

public interface IDeviceRegistry : IAsyncDisposable, IDisposable
{
    IReadOnlyCollection<IDevice> Devices { get; }

    bool TryAdd(IDevice device);

    void AddOrUpdate(IDevice device);

    bool Remove(string deviceId, bool dispose = false);

    bool TryGetDevice(string deviceId, out IDevice device);

    bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class;

    IDevice GetRequiredDevice(string deviceId);

    TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class;

    IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class;
}
