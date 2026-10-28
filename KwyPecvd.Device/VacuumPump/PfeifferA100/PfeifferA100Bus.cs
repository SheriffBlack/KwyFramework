using Kwy.Communicate.Abstractions;

namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

/// <summary>通过 Kwy TCP/Serial 字节传输执行 Pfeiffer A100 请求。</summary>
public sealed class PfeifferA100Bus : IPfeifferA100Bus
{
    private const int MaximumResponseLength = 256;
    private readonly IByteStreamClient transport;
    private readonly SemaphoreSlim transactionGate = new(1, 1);

    public PfeifferA100Bus(IByteStreamClient transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        transport.IsConnected ? Task.CompletedTask : transport.ConnectAsync(cancellationToken);

    public async Task WriteRunningAsync(
        string address,
        bool running,
        CancellationToken cancellationToken = default)
    {
        byte[] request = PfeifferA100Protocol.CreateRunningRequest(address, running);
        byte[] response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        PfeifferA100Protocol.ValidateCommandResponse(response, address);
    }

    public async Task<PfeifferA100Status> ReadStatusAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        byte[] request = PfeifferA100Protocol.CreateStatusRequest(address);
        byte[] response = await ExchangeAsync(request, cancellationToken).ConfigureAwait(false);
        return PfeifferA100Protocol.ParseStatusResponse(response, address);
    }

    private async Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken)
    {
        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!transport.IsConnected)
                throw new InvalidOperationException("Pfeiffer A100 transport is not connected.");

            await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            return await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    private async Task<byte[]> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var response = new List<byte>();
        var buffer = new byte[1];

        while (response.Count < MaximumResponseLength)
        {
            int read = await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                throw new EndOfStreamException(
                    "Pfeiffer A100 transport closed before a complete response was received.");

            if (buffer[0] == (byte)'\n' && response.Count == 0)
                continue;

            response.Add(buffer[0]);
            if (buffer[0] == (byte)'\r')
                return response.ToArray();
        }

        throw new FormatException("Pfeiffer A100 response exceeds the maximum frame length.");
    }
}
