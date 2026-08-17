using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Core.Instrument;

namespace Kwy.Device.Instruments.Lcr;

public class ZX :
    InstrumentBase,
    IMeasurementInstrument
{
    private const string DefaultModel = "ADEX_DCR";

    public override string DeviceModel => DefaultModel;


    public ZX(string deviceId, string deviceName, IProtocolConfig protocolConfig, ICommunicationFactory? factory = null)
    : base(deviceId, deviceName, protocolConfig, factory)
    {
    }

    public ZX(string deviceId, string deviceName, ICommunicationClient protocol)
        : base(deviceId, deviceName, protocol)
    {
    }


    public ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
