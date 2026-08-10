using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.Scanners;

public interface IBarcodeScannerDevice : IDevice, IConfigurableDevice
{
    event EventHandler<BarcodeScannedEventArgs>? CodeReceived;

    string? LastCode { get; }

    Task TriggerScanAsync(CancellationToken cancellationToken = default);

    Task<string> WaitForCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}