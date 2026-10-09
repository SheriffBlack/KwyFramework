namespace KwyPecvd.Device;

public abstract class HardwareComponentBase : IHardwareComponent
{
    protected HardwareComponentBase(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    public string Id { get; }

    public abstract Task InitializeAsync(
        CancellationToken cancellationToken = default);

    public abstract Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default);

    public virtual Task ResetAsync(
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task ShutdownAsync(
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
