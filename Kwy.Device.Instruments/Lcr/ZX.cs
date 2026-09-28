using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;
using Kwy.Device.Instrument.Abstractions;
using Kwy.Device.Instrument.Core;

namespace Kwy.Device.Instruments.Lcr;

public class ZX : InstrumentBase, IMeasurementInstrument
{
    public ZX(string deviceId, string deviceName, IDeviceConfig deviceParameter, IProtocolConfig protocolConfig, ICommunicationFactory? factory = null)
    : base(deviceId, deviceName, deviceParameter, protocolConfig, factory)
    {
    }

    public ZX(string deviceId, string deviceName, IDeviceConfig deviceParameter, ICommunicationClient protocol)
        : base(deviceId, deviceName, deviceParameter, protocol)
    {
    }

    public ValueTask<InstrumentMeasurementResult> ReadMeasurementAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
