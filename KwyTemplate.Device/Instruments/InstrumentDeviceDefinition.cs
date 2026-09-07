using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;

using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Instruments;

public abstract class InstrumentDeviceDefinition : DeviceDefinition
{
    protected InstrumentDeviceDefinition(
        string deviceId,
        string deviceName,
        IProtocolConfig connectionConfig,
        IDeviceConfig? deviceParameter)
        : base(deviceId, deviceName)
    {
        ConnectionConfig = connectionConfig ?? throw new ArgumentNullException(nameof(connectionConfig));
        DeviceParameter = deviceParameter;
    }

    public IProtocolConfig ConnectionConfig { get; }

    public IDeviceConfig? DeviceParameter { get; }

    protected void ValidateConnection()
    {
        if (!ConnectionConfig.Validate())
        {
            throw new InvalidOperationException($"Device {DeviceId} connection configuration is invalid.");
        }
    }
}


