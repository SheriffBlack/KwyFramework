using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.MarkPrinters;

public sealed class MarkPrintDeviceDefinition : DeviceDefinition
{
    private readonly MarkPrintConfig config;

    public MarkPrintDeviceDefinition(string deviceId, string deviceName, MarkPrintConfig config)
        : base(deviceId, deviceName)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        if (!config.Validate())
        {
            throw new InvalidOperationException($"Device {DeviceId} mark printer configuration is invalid.");
        }

        return new TcpMarkPrintDevice(
            DeviceId,
            DeviceName,
            config,
            GetRequiredService<ICommunicationFactory>(services));
    }
}
