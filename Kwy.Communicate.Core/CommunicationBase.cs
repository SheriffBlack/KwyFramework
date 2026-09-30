using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Core;

/// <summary>
/// 主动读取字节流传输的基类。
/// </summary>
public abstract class CommunicationBase : CommunicationClientBase, IByteTransport
{
    private readonly SemaphoreSlim readSemaphore = new(1, 1);
    private readonly SemaphoreSlim writeSemaphore = new(1, 1);

    protected CommunicationBase(IProtocolConfig config) : base(config)
    {
    }

    protected abstract Task ConnectInternalAsync(CancellationToken cancellationToken);
    protected abstract Task DisconnectInternalAsync(CancellationToken cancellationToken);
    protected abstract Task SendInternalAsync(byte[] data, CancellationToken cancellationToken);
    protected abstract Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    protected abstract bool ValidateConnection();

    protected sealed override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ConnectInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                readSemaphore.Release();
            }
        }
        finally
        {
            writeSemaphore.Release();
        }
    }

    protected sealed override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                readSemaphore.Release();
            }
        }
        finally
        {
            writeSemaphore.Release();
        }
    }

    protected sealed override bool IsConnectionAlive()
        => ValidateConnection();

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        if (!IsConnected)
            throw new InvalidOperationException("The transport is not connected.");

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        CancellationToken operationToken = linkedCancellation.Token;
        await writeSemaphore.WaitAsync(operationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("The transport disconnected before the write could start.");

            await SendInternalAsync(data.ToArray(), operationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"Write failed: {ex.Message}");
            throw;
        }
        finally
        {
            writeSemaphore.Release();
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty)
            throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));
        if (!IsConnected)
            throw new InvalidOperationException("The transport is not connected.");

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        CancellationToken operationToken = linkedCancellation.Token;
        await readSemaphore.WaitAsync(operationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("The transport disconnected before the read could start.");

            return await ReceiveInternalAsync(buffer, operationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"Read failed: {ex.Message}");
            throw;
        }
        finally
        {
            readSemaphore.Release();
        }
    }

}
