using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Lcr;
using KwyTemplate.Device.Devices;

namespace KwyTemplate.Device.Instruments;

public sealed class HiokiLcrDeviceDefinition : InstrumentDeviceDefinition
{
    public HiokiLcrDeviceDefinition(
        string deviceId,
        string deviceName,
        IProtocolConfig connectionConfig,
        HiokiLcrConfig? parameter = null)
        : base(deviceId, deviceName, connectionConfig, parameter ?? new HiokiLcrConfig())
    {
    }

    public override IDevice CreateDevice(IServiceProvider services)
    {
        ValidateConnection();
        var instrument = new HiokiLcr(
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


