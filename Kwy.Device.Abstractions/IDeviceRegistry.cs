namespace Kwy.Device.Abstractions;

/// <summary>
/// 应用程序设备的非所有者索引。
/// 创建设备的组件仍需负责将其断开并释放。
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
