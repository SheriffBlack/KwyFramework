using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Core;
using Kwy.Communicate.Secs;

namespace Kwy.Communicate.Secs.Secs4Net;

public sealed class Secs4NetSecsClient : CommunicationClientBase, ISecsClient
{
    private readonly global::Secs4Net.ISecsGem secsGem;
    private readonly SecsHsmsConfig secsConfig;
    private CancellationTokenSource? receiveCancellation;
    private Task? receiveTask;

    public Secs4NetSecsClient(global::Secs4Net.ISecsGem secsGem, SecsHsmsConfig? config = null)
        : base(config ?? new SecsHsmsConfig { DeviceId = secsGem?.DeviceId ?? 0 })
    {
        this.secsGem = secsGem ?? throw new ArgumentNullException(nameof(secsGem));
        secsConfig = (SecsHsmsConfig)Config;
    }

    public HsmsSessionState SessionState => IsConnectionAlive()
        ? HsmsSessionState.Selected
        : HsmsSessionState.NotConnected;

    public event EventHandler<SecsMessageReceivedEventArgs>? PrimaryMessageReceived;

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await WaitUntilSecs4NetConnectedAsync(cancellationToken);

        receiveCancellation?.Cancel();
        receiveCancellation?.Dispose();
        // The receive loop is part of the connected session lifetime, not the short ConnectAsync token.
        receiveCancellation = new CancellationTokenSource();
        receiveTask = ReceivePrimaryMessagesAsync(receiveCancellation.Token);
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        receiveCancellation?.Cancel();

        if (receiveTask is not null)
        {
            try
            {
                var timeout = TimeSpan.FromMilliseconds(Math.Max(1, secsConfig.Timeout));
                await receiveTask.WaitAsync(timeout, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException ex)
            {
                OnErrorOccurred(ex, $"Timed out waiting for Secs4Net receive loop to stop: {ex.Message}");
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, $"Secs4Net receive loop stopped with error: {ex.Message}");
            }
        }

        receiveTask = null;
        receiveCancellation?.Dispose();
        receiveCancellation = null;
    }

    protected override bool IsConnectionAlive()
    {
        if (secsGem is global::Secs4Net.ISecsConnection connection)
        {
            return IsSelectedOrConnected(connection.State);
        }

        return receiveTask is { IsCompleted: false };
    }

    public async Task SendAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        EnsureConnected();

        try
        {
            using global::Secs4Net.SecsMessage secs4NetMessage = Secs4NetMessageConverter.ToSecs4Net(message);
            await secsGem.SendAsync(secs4NetMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"SECS send failed: {ex.Message}");
            throw;
        }
    }

    public async Task<SecsMessage> SendPrimaryAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        EnsureConnected();

        if (!message.ReplyExpected)
        {
            throw new ArgumentException("Primary message must expect reply.", nameof(message));
        }

        try
        {
            using global::Secs4Net.SecsMessage secs4NetMessage = Secs4NetMessageConverter.ToSecs4Net(message);
            using global::Secs4Net.SecsMessage? reply = await secsGem.SendAsync(secs4NetMessage, cancellationToken);
            return reply is null
                ? new SecsMessage(message.Stream, checked((byte)(message.Function + 1)), SystemBytes: message.SystemBytes)
                : Secs4NetMessageConverter.FromSecs4Net(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"SECS primary request failed: {ex.Message}");
            throw;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await base.DisposeAsync();
        if (secsGem is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (secsGem is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task WaitUntilSecs4NetConnectedAsync(CancellationToken cancellationToken)
    {
        if (secsGem is not global::Secs4Net.ISecsConnection connection)
        {
            return;
        }

        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, secsConfig.Timeout));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!IsSelectedOrConnected(connection.State))
        {
            await Task.Delay(100, timeoutCts.Token);
        }
    }

    private async Task ReceivePrimaryMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var wrapper in secsGem.GetPrimaryMessageAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                using global::Secs4Net.SecsMessage primary = wrapper.PrimaryMessage;
                PrimaryMessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs(Secs4NetMessageConverter.FromSecs4Net(primary)));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await HandleCommunicationFailureAsync(ex, $"Secs4Net primary message loop failed: {ex.Message}");
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("SECS client is not connected.");
        }
    }

    private static bool IsSelectedOrConnected(global::Secs4Net.ConnectionState state)
    {
        var name = state.ToString();
        return string.Equals(name, "Selected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Connected", StringComparison.OrdinalIgnoreCase);
    }
}
