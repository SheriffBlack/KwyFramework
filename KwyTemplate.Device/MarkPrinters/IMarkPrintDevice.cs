using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.MarkPrinters;

public interface IMarkPrintDevice : IDevice, IConfigurableDevice
{
    Task SetPrintStringAsync(string printString, CancellationToken cancellationToken = default);
}
