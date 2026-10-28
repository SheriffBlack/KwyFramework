using Kwy.Communicate.Abstractions;

namespace KwyPecvd.Device.RfPowerSupply.Mks;

/// <summary>通过 Kwy TCP/Serial 字节传输执行 MKS RF 请求。</summary>
public sealed class MksRfPowerBus : IMksRfPowerBus
{
    private const int MaximumResponseLength = 256;
    private readonly IByteStreamClient transport;
    private readonly SemaphoreSlim transactionGate = new(1, 1);

    public MksRfPowerBus(IByteStreamClient transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        transport.IsConnected ? Task.CompletedTask : transport.ConnectAsync(cancellationToken);

    public async Task WritePowerAsync(
        double power,
        CancellationToken cancellationToken = default)
    {
        byte[] response = await ExchangeAsync(
                MksRfPowerProtocol.CreateSetPowerRequest(power),
                cancellationToken)
            .ConfigureAwait(false);
        MksRfPowerProtocol.ValidateCommandResponse(response);
    }

    public async Task WriteEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        byte[] response = await ExchangeAsync(
                MksRfPowerProtocol.CreateEnableRequest(enabled),
                cancellationToken)
            .ConfigureAwait(false);
        MksRfPowerProtocol.ValidateCommandResponse(response);
    }

    public async Task<MksRfPowerStatus> ReadStatusAsync(
        CancellationToken cancellationToken = default)
    {
        byte[] response = await ExchangeAsync(
                MksRfPowerProtocol.CreateStatusRequest(),
                cancellationToken)
            .ConfigureAwait(false);
        return MksRfPowerProtocol.ParseStatusResponse(response);
    }

    private async Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken)
    {
        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!transport.IsConnected)
                throw new InvalidOperationException("MKS RF transport is not connected.");

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
                    "MKS RF transport closed before a complete response was received.");

            if (buffer[0] == (byte)'\n' && response.Count == 0)
                continue;

            response.Add(buffer[0]);
            if (buffer[0] == (byte)'\r')
                return response.ToArray();
        }

        throw new FormatException("MKS RF response exceeds the maximum frame length.");
    }
}
