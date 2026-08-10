using Kwy.Device.Abstractions;
using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Scanners;

public sealed class BarcodeScannerDeviceDefinition : DeviceDefinition
{
    private readonly BarcodeScannerConfig config;

    public BarcodeScannerDeviceDefinition(string deviceId, string deviceName, BarcodeScannerConfig config)
        : base(deviceId, deviceName)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        if (!config.Validate())
        {
            throw new InvalidOperationException($"Device {DeviceId} barcode scanner configuration is invalid.");
        }

        return new SerialBarcodeScannerDevice(
            DeviceId,
            DeviceName,
            config);
    }
}
