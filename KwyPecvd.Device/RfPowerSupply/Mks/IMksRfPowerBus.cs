namespace KwyPecvd.Device.RfPowerSupply.Mks;

public interface IMksRfPowerBus
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task WritePowerAsync(double power, CancellationToken cancellationToken = default);

    Task WriteEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<MksRfPowerStatus> ReadStatusAsync(CancellationToken cancellationToken = default);
}