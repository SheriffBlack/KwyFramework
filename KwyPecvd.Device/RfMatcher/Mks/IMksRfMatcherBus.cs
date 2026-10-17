namespace KwyPecvd.Device.RfMatcher.Mks;

public interface IMksRfMatcherBus
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task WriteModeAsync(
        byte address,
        byte network,
        RfMatcherMode mode,
        CancellationToken cancellationToken = default);

    Task WriteManualPositionsAsync(
        byte address,
        byte network,
        double tunePosition,
        double loadPosition,
        CancellationToken cancellationToken = default);

    Task StopMotionAsync(
        byte address,
        byte network,
        CancellationToken cancellationToken = default);

    Task<MksRfMatcherStatus> ReadStatusAsync(
        byte address,
        byte network,
        CancellationToken cancellationToken = default);
}
