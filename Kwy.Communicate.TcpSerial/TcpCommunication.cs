using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;
using System.Net.Sockets;

namespace Kwy.Communicate.TcpSerial;

/// <summary>
/// Active-read TCP byte transport.
/// </summary>
public sealed class TcpCommunication : CommunicationBase
{
    private readonly TcpConfig tcpConfig;
    private TcpClient? tcpClient;
    private NetworkStream? stream;

    public TcpCommunication(TcpConfig config) : base(config)
    {
        tcpConfig = config ?? throw new ArgumentNullException(nameof(config));
    }

    protected override async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        tcpClient = new TcpClient
        {
            ReceiveBufferSize = tcpConfig.ReceiveBufferSize,
            SendBufferSize = tcpConfig.SendBufferSize,
            ReceiveTimeout = tcpConfig.ReceiveTimeout,
            SendTimeout = tcpConfig.SendTimeout
        };

        if (tcpConfig.KeepAlive)
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(tcpConfig.Timeout);
        await tcpClient.ConnectAsync(tcpConfig.Host, tcpConfig.Port, timeout.Token);
        stream = tcpClient.GetStream();
    }

    protected override Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        stream?.Dispose();
        tcpClient?.Dispose();
        stream = null;
        tcpClient = null;
        return Task.CompletedTask;
    }

    protected override async Task SendInternalAsync(byte[] data, CancellationToken cancellationToken)
    {
        if (stream == null || !stream.CanWrite)
            throw new InvalidOperationException("TCP stream is not writable.");

        await stream.WriteAsync(data, cancellationToken);
    }

    protected override async Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (stream == null || !stream.CanRead)
            throw new InvalidOperationException("TCP stream is not readable.");

        var length = await stream.ReadAsync(buffer, cancellationToken);
        if (length == 0)
            throw new IOException("The remote TCP endpoint closed the connection.");

        return length;
    }

    protected override bool ValidateConnection()
    {
        if (tcpClient?.Client is not { } socket || stream is not { CanRead: true, CanWrite: true })
            return false;

        return socket.Connected && !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
    }
}
