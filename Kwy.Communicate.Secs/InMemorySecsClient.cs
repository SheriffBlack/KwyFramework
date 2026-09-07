using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Communicate.Secs;

public sealed class InMemorySecsClient : ISecsClient
{
    private readonly Queue<SecsMessage> responses = new();

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public bool IsConnected => State == ConnectionState.Connected;

    public IProtocolConfig Config { get; }

    public HsmsSessionState SessionState { get; private set; } = HsmsSessionState.NotConnected;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    public event EventHandler<SecsMessageReceivedEventArgs>? PrimaryMessageReceived;

    public InMemorySecsClient(SecsHsmsConfig? config = null)
    {
        Config = config ?? new SecsHsmsConfig();
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(ConnectionState.Connected);
        SessionState = HsmsSessionState.Selected;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SessionState = HsmsSessionState.NotConnected;
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public Task SendAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfNotConnected();
        if (message.IsPrimary)
        {
            PrimaryMessageReceived?.Invoke(this, new SecsMessageReceivedEventArgs(message));
        }

        return Task.CompletedTask;
    }

    public Task<SecsMessage> SendPrimaryAsync(SecsMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfNotConnected();
        if (!message.ReplyExpected)
        {
            throw new ArgumentException("Primary message must expect reply.", nameof(message));
        }

        return Task.FromResult(responses.Count > 0
            ? responses.Dequeue()
            : new SecsMessage(message.Stream, checked((byte)(message.Function + 1)), SystemBytes: message.SystemBytes));
    }

    public void EnqueueResponse(SecsMessage response) => responses.Enqueue(response);

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void SetState(ConnectionState target)
    {
        var previous = State;
        State = target;
        if (previous != target)
        {
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previous, target));
        }
    }

    private void ThrowIfNotConnected()
    {
        if (!IsConnected)
        {
            var ex = new InvalidOperationException("SECS client is not connected.");
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex, ex.Message));
            throw ex;
        }
    }
}
