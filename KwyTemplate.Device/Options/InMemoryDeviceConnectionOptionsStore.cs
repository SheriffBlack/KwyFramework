namespace KwyTemplate.Device.Options;

public sealed class InMemoryDeviceConnectionOptionsStore : IDeviceConnectionOptionsStore
{
    private readonly object syncRoot = new();
    private DeviceConnectionOptions options = new();

    public ValueTask<DeviceConnectionOptions> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            DeviceConnectionOptionsNormalizer.Normalize(options);
            return ValueTask.FromResult(DeviceConnectionOptionsCloner.Clone(options));
        }
    }

    public ValueTask SaveAsync(DeviceConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            this.options = DeviceConnectionOptionsCloner.Clone(options);
            DeviceConnectionOptionsNormalizer.Normalize(this.options);
        }

        return ValueTask.CompletedTask;
    }
}
