using Kwy.Communicate.Core;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public sealed class SecsGemClient : CommunicationClientBase, ISecsGemClient
{
    private readonly ISecsGem secsGem;
    private readonly ISecsConnection hsmsConnection;
    private readonly SecsGemClientConfig secsConfig;
    private readonly bool ownsSecs4NetDependencies;
    private CancellationTokenSource? sessionCancellation;

    public SecsGemClient(
        ISecsGem secsGem,
        ISecsConnection hsmsConnection,
        SecsGemClientConfig config)
        : this(secsGem, hsmsConnection, config, ownsSecs4NetDependencies: false)
    {
    }

    internal SecsGemClient(
        ISecsGem secsGem,
        ISecsConnection hsmsConnection,
        SecsGemClientConfig config,
        bool ownsSecs4NetDependencies)
        : base(config)
    {
        this.secsGem = secsGem ?? throw new ArgumentNullException(nameof(secsGem));
        this.hsmsConnection = hsmsConnection ?? throw new ArgumentNullException(nameof(hsmsConnection));
        secsConfig = config ?? throw new ArgumentNullException(nameof(config));
        this.ownsSecs4NetDependencies = ownsSecs4NetDependencies;
    }

    public ConnectionState HsmsState => hsmsConnection.State;

    public IAsyncEnumerable<PrimaryMessageWrapper> GetPrimaryMessagesAsync(
        CancellationToken cancellationToken = default)
        => secsGem.GetPrimaryMessageAsync(cancellationToken);

    public async Task<SecsMessage?> SendAsync(
        SecsMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        EnsureSelected();

        try
        {
            return await secsGem.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"SECS send failed: {ex.Message}");
            throw;
        }
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
        sessionCancellation = new CancellationTokenSource();
        hsmsConnection.Start(sessionCancellation.Token);

        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, secsConfig.Timeout));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (hsmsConnection.State != ConnectionState.Selected)
        {
            await Task.Delay(100, timeoutCts.Token);
        }
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
        sessionCancellation = null;
        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive() => hsmsConnection.State == ConnectionState.Selected;

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await base.DisposeAsync().ConfigureAwait(false);

        if (!ownsSecs4NetDependencies)
        {
            return;
        }

        await DisposeDependencyAsync(secsGem).ConfigureAwait(false);
        if (!ReferenceEquals(secsGem, hsmsConnection))
        {
            await DisposeDependencyAsync(hsmsConnection).ConfigureAwait(false);
        }
    }

    private void EnsureSelected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("The SECS/HSMS session is not selected.");
        }
    }

    private static async ValueTask DisposeDependencyAsync(object dependency)
    {
        if (dependency is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (dependency is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
