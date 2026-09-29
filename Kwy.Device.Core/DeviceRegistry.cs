using System.Collections.Concurrent;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Core;

/// <summary>
/// 应用已创建设备实例的注册表。
/// 按稳定设备 ID 和能力接口查找实例，不负责创建设备、加载配置或处理任何领域语义。
/// </summary>
public sealed class DeviceRegistry : IDeviceRegistry
{
    private readonly ConcurrentDictionary<string, IDevice> devices = new(StringComparer.OrdinalIgnoreCase);

    public DeviceRegistry(IEnumerable<IDevice>? devices = null)
    {
        foreach (IDevice device in devices ?? [])
        {
            Add(device);
        }
    }

    public IReadOnlyCollection<IDevice> Devices
    {
        get
        {
            return devices.Values.ToArray();
        }
    }

    public void Add(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.DeviceId);

        if (devices.TryAdd(device.DeviceId, device))
        {
            return;
        }

        if (devices.TryGetValue(device.DeviceId, out IDevice? existing)
            && ReferenceEquals(existing, device))
        {
            return;
        }

        throw new InvalidOperationException($"A device with id '{device.DeviceId}' is already registered.");
    }

    public bool TryGetDevice(string deviceId, out IDevice device)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return devices.TryGetValue(deviceId, out device!);
    }

    public bool TryGetDevice<TCapability>(string deviceId, out TCapability device)
        where TCapability : class
    {
        if (TryGetDevice(deviceId, out IDevice found) && found is TCapability typed)
        {
            device = typed;
            return true;
        }

        device = default!;
        return false;
    }

    public IDevice GetRequiredDevice(string deviceId)
    {
        return TryGetDevice(deviceId, out IDevice device)
            ? device
            : throw new KeyNotFoundException($"Device not found: {deviceId}");
    }

    public TCapability GetRequiredDevice<TCapability>(string deviceId)
        where TCapability : class
    {
        IDevice device = GetRequiredDevice(deviceId);
        return device as TCapability
            ?? throw new InvalidOperationException(
                $"Device {deviceId} is {device.GetType().FullName}, not {typeof(TCapability).FullName}.");
    }

    public IReadOnlyCollection<TCapability> GetDevices<TCapability>()
        where TCapability : class
    {
        return devices.Values.OfType<TCapability>().ToArray();
    }
}
