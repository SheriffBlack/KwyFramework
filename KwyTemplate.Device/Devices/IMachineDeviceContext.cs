using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.Devices;

/// <summary>
/// 通过设备ID为机器流提供对设备的类型化访问。
/// Flow 代码使用此上下文，而不是直接创建设备。
/// </summary>
public interface IMachineDeviceContext
{
    IReadOnlyCollection<IDevice> Devices { get; }

    bool TryGet<TDevice>(string deviceId, out TDevice? device)
        where TDevice : class;

    TDevice GetRequired<TDevice>(string deviceId)
        where TDevice : class;

    IReadOnlyCollection<TDevice> GetAll<TDevice>()
        where TDevice : class;
}

public sealed class MachineDeviceContext : IMachineDeviceContext
{
    private readonly IDeviceRegistry registry;

    public MachineDeviceContext(IDeviceRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyCollection<IDevice> Devices => registry.Devices;

    public bool TryGet<TDevice>(string deviceId, out TDevice? device)
        where TDevice : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (registry.TryGetDevice<TDevice>(deviceId, out TDevice found))
        {
            device = found;
            return true;
        }

        device = null;
        return false;
    }

    public TDevice GetRequired<TDevice>(string deviceId)
        where TDevice : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return registry.GetRequiredDevice<TDevice>(deviceId);
    }

    public IReadOnlyCollection<TDevice> GetAll<TDevice>()
        where TDevice : class
    {
        return registry.GetDevices<TDevice>();
    }
}

