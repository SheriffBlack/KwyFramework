using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

/// <summary>
/// Base class for active-read byte stream transports.
/// </summary>
public abstract class CommunicationBase : CommunicationClientBase, IByteTransport
{
    protected CommunicationBase(IProtocolConfig config) : base(config)
    {
    }

    protected abstract Task ConnectInternalAsync(CancellationToken cancellationToken);
    protected abstract Task DisconnectInternalAsync(CancellationToken cancellationToken);
    protected abstract Task SendInternalAsync(byte[] data, CancellationToken cancellationToken);
    protected abstract Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    protected abstract bool ValidateConnection();

    protected sealed override Task ConnectCoreAsync(CancellationToken cancellationToken)
        => ConnectInternalAsync(cancellationToken);

    protected sealed override Task DisconnectCoreAsync(CancellationToken cancellationToken)
        => DisconnectInternalAsync(cancellationToken);

    protected sealed override bool IsConnectionAlive()
        => ValidateConnection();

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        if (!IsConnected)
            throw new InvalidOperationException("The transport is not connected.");

        try
        {
            await SendInternalAsync(data.ToArray(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"Write failed: {ex.Message}");
            throw;
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty)
            throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));
        if (!IsConnected)
            throw new InvalidOperationException("The transport is not connected.");

        try
        {
            return await ReceiveInternalAsync(buffer, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"Read failed: {ex.Message}");
            throw;
        }
    }

}
