using Kwy.Device.Abstractions;
using Kwy.Device.PLCs.Hsl;

using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Plcs;

public sealed class HslPlcDeviceDefinition : DeviceDefinition
{
    public HslPlcDeviceDefinition(
        string deviceId,
        string deviceName,
        HslPlcConfig config)
        : base(deviceId, deviceName)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public HslPlcConfig Config { get; }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        if (!Config.Validate())
        {
            throw new InvalidOperationException($"Device {DeviceId} HSL PLC configuration is invalid.");
        }

        return new HslPlcDevice(DeviceId, DeviceName, Config);
    }
}


