using Kwy.Communicate.Abstractions;

namespace KwyPecvd.Device.Thermal.TemperatureController.Omron;

/// <summary>通过 Kwy TCP/Serial 字节传输执行 EJ1 Modbus RTU 请求。</summary>
public sealed class OmronEj1Bus : IOmronEj1Bus
{
    private readonly IByteTransport transport;
    private readonly SemaphoreSlim transactionGate = new(1, 1);

    public OmronEj1Bus(IByteTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        transport.IsConnected ? Task.CompletedTask : transport.ConnectAsync(cancellationToken);

    public async Task<short> ReadHoldingRegisterAsync(
        byte unitAddress,
        ushort register,
        CancellationToken cancellationToken = default)
    {
        byte[] request = OmronEj1Protocol.CreateReadRequest(unitAddress, register);
        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            byte[] response = await ReadResponseAsync(
                    successLength: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            return OmronEj1Protocol.ParseReadResponse(response, unitAddress);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    public async Task WriteSingleRegisterAsync(
        byte unitAddress,
        ushort register,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        byte[] request = OmronEj1Protocol.CreateWriteRequest(unitAddress, register, value);
        await transactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            await transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            byte[] response = await ReadResponseAsync(
                    successLength: 8,
                    cancellationToken)
                .ConfigureAwait(false);
            OmronEj1Protocol.ValidateWriteResponse(response, request, unitAddress);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    private void EnsureConnected()
    {
        if (!transport.IsConnected)
            throw new InvalidOperationException("Omron EJ1 transport is not connected.");
    }

    private async ValueTask ReadExactlyAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            int count = await transport.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (count <= 0)
                throw new EndOfStreamException(
                    "Omron EJ1 transport closed before a complete response was received.");

            offset += count;
        }
    }

    private async ValueTask<byte[]> ReadResponseAsync(
        int successLength,
        CancellationToken cancellationToken)
    {
        var header = new byte[3];
        await ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        int frameLength = (header[1] & 0x80) != 0
            ? 5
            : successLength;

        var response = new byte[frameLength];
        header.CopyTo(response, 0);
        await ReadExactlyAsync(
                response.AsMemory(header.Length),
                cancellationToken)
            .ConfigureAwait(false);
        return response;
    }
}
