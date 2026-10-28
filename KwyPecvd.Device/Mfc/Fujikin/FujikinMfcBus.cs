using Kwy.Communicate.Abstractions;

namespace KwyPecvd.Device.Mfc.Fujikin;

/// <summary>基于 Kwy 字节传输抽象的 Fujikin MFC 通信总线。</summary>
public sealed class FujikinMfcBus : IFujikinMfcBus
{
    private readonly IByteStreamClient transport;
    private readonly SemaphoreSlim transactionGate = new(1, 1);

    public FujikinMfcBus(IByteStreamClient transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        transport.IsConnected
            ? Task.CompletedTask
            : transport.ConnectAsync(cancellationToken);

    public async Task WriteSetPointAsync(
        byte address,
        short rawSetPoint,
        CancellationToken cancellationToken = default)
    {
        byte[] request = FujikinMfcProtocol.CreateWriteSetPointRequest(address, rawSetPoint);

        await WriteCommandAsync(request, "set-point", cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteRampTimeAsync(
        byte address,
        int rawRampTime,
        CancellationToken cancellationToken = default)
    {
        byte[] request = FujikinMfcProtocol.CreateWriteRampTimeRequest(address, rawRampTime);

        await WriteCommandAsync(request, "ramp-time", cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteCommandAsync(
        byte[] request,
        string commandName,
        CancellationToken cancellationToken)
    {

        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            byte response = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (response == FujikinMfcProtocol.NegativeAcknowledge)
                throw new InvalidOperationException($"Fujikin MFC rejected the {commandName} command.");
            if (response != FujikinMfcProtocol.Acknowledge)
                throw new FormatException($"Unexpected Fujikin command response 0x{response:X2}.");
        }
        finally
        {
            transactionGate.Release();
        }
    }

    public async Task<short> ReadIndicatedFlowAsync(
        byte address,
        CancellationToken cancellationToken = default)
    {
        byte[] request =
            FujikinMfcProtocol.CreateReadIndicatedFlowRequest(address);

        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            byte firstByte = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (firstByte == FujikinMfcProtocol.NegativeAcknowledge)
                throw new InvalidOperationException("Fujikin MFC rejected the indicated-flow query.");

            // Some controllers acknowledge a query before returning its data frame.
            if (firstByte == FujikinMfcProtocol.Acknowledge)
                firstByte = await ReadByteAsync(cancellationToken).ConfigureAwait(false);

            var header = new byte[4];
            header[0] = firstByte;
            await ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);

            int frameLength = FujikinMfcProtocol.GetFrameLength(header);
            var response = new byte[frameLength];
            header.CopyTo(response, 0);
            await ReadExactlyAsync(response.AsMemory(header.Length), cancellationToken).ConfigureAwait(false);

            return FujikinMfcProtocol.ParseIndicatedFlowResponse(response, address);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    private void EnsureConnected()
    {
        if (!transport.IsConnected)
            throw new InvalidOperationException("Fujikin MFC transport is not connected.");
    }

    private async ValueTask<byte> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer[0];
    }

    private async ValueTask ReadExactlyAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await transport
                .ReadAsync(buffer[offset..], cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0)
                throw new EndOfStreamException("Fujikin MFC closed the transport before a complete response was received.");

            offset += read;
        }
    }
}
