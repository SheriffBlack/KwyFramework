using Kwy.Device.Abstractions;
using Kwy.Device.IoCards.Advantech;

using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.IoCards;

public sealed class AdvantechIoCardDeviceDefinition : DeviceDefinition
{
    public AdvantechIoCardDeviceDefinition(
        string deviceId,
        string deviceName,
        AdvantechIoCardConfig config)
        : base(deviceId, deviceName)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public AdvantechIoCardConfig Config { get; }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        if (!Config.Validate())
        {
            throw new InvalidOperationException($"Device {DeviceId} Advantech IO card configuration is invalid.");
        }

        return new AdvantechIoCardDevice(DeviceId, DeviceName, Config);
    }
}

