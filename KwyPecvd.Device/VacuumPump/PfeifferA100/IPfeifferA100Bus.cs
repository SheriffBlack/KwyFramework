namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

public interface IPfeifferA100Bus
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task WriteRunningAsync(
        string address,
        bool running,
        CancellationToken cancellationToken = default);

    Task<PfeifferA100Status> ReadStatusAsync(
        string address,
        CancellationToken cancellationToken = default);
}