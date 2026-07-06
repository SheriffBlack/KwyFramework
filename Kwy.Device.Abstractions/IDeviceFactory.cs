namespace Kwy.Device.Abstractions;

public interface IDeviceFactory
{
    void Register<TConfig, TDevice>(Func<TConfig, TDevice> factory)
        where TConfig : IDeviceConfig
        where TDevice : IDevice;

    IDevice Create(IDeviceConfig config);

    TDevice Create<TDevice>(IDeviceConfig config)
        where TDevice : IDevice;

    IReadOnlyCollection<DeviceRegistration> GetRegistrations();
}
