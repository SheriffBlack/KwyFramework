namespace Kwy.Device.Abstractions;

/// <summary>
/// Non-owning index of application devices. The component that creates a device remains
/// responsible for disconnecting and disposing it.
/// </summary>
public interface IDeviceRegistry
{
    IReadOnlyCollection<IDevice> Devices { get; }

    void Add(IDevice device);

    bool TryGetDevice(string deviceId, out IDevice device);

    bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class;

    IDevice GetRequiredDevice(string deviceId);

    TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class;

    IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class;
}
