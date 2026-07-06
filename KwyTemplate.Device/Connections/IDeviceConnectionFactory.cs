using Kwy.Device.Abstractions;
using KwyTemplate.Device.Options;

namespace KwyTemplate.Device.Connections;

public interface IDeviceConnectionFactory
{
    string DeviceType { get; }

    IDevice Create(DeviceConnectionEntry entry);

    bool IsSameDevice(IDevice device, DeviceConnectionEntry entry);

    DeviceConnectionConfigurationSection CreateConfigurationSection(DeviceConnectionEntry entry);
}
