using Secs4Net;

namespace Kwy.Communicate.Gem.Testing;

/// <summary>Host 验收场景使用的最小传输契约。</summary>
public interface IGemHostTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default);

    /// <summary>持续读取 Equipment 主动发起的 Primary Message。</summary>
    IAsyncEnumerable<PrimaryMessageWrapper> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>将生产传输适配为 Host 验收传输，不改变其生命周期语义。</summary>
public sealed class SecsGemHostTransport : IGemHostTransport
{
    private readonly ISecsGemClient client;
    private readonly bool ownsClient;

    public SecsGemHostTransport(ISecsGemClient client, bool ownsClient = false)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.ownsClient = ownsClient;
    }

    public bool IsConnected => client.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
        => client.ConnectAsync(cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
        => client.DisconnectAsync(cancellationToken);

    public Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default)
        => client.SendAsync(message, cancellationToken);

    public IAsyncEnumerable<PrimaryMessageWrapper> ReadAllAsync(
        CancellationToken cancellationToken = default)
        => client.ReadAllAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (ownsClient)
            await client.DisposeAsync().ConfigureAwait(false);
    }
}
