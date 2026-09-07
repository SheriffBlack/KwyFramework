using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Dcr;
using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Instruments;

public sealed class AdexDcrDeviceDefinition : InstrumentDeviceDefinition
{
    public AdexDcrDeviceDefinition(
        string deviceId,
        string deviceName,
        IProtocolConfig connectionConfig,
        AdexDcrConfig? parameter = null)
        : base(deviceId, deviceName, connectionConfig, parameter ?? new AdexDcrConfig())
    {
    }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        ValidateConnection();
        var instrument = new AdexDcr(
            DeviceId,
            DeviceName,
            ConnectionConfig,
            GetRequiredService<ICommunicationFactory>(services));
        if (DeviceParameter is not null)
        {
            instrument.DeviceParameter = DeviceParameter;
        }

        return instrument;
    }
}


