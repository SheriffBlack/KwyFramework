using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.Devices;

/// <summary>
/// Provides machine flows with typed access to devices by DeviceId.
/// Flow code uses this context instead of creating devices directly.
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

