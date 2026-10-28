using Kwy.Communicate.Abstractions;

namespace KwyPecvd.Device.RfMatcher.Mks;

/// <summary>通过 Kwy TCP/Serial 字节传输执行 MKS 匹配器请求。</summary>
public sealed class MksRfMatcherBus : IMksRfMatcherBus
{
    private readonly IByteStreamClient transport;

    private readonly SemaphoreSlim transactionGate = new(1, 1);

    public MksRfMatcherBus(IByteStreamClient transport)
    {
        this.transport = transport ??
            throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => transport.IsConnected;

    public Task ConnectAsync(
        CancellationToken cancellationToken = default) =>
        transport.IsConnected
            ? Task.CompletedTask
            : transport.ConnectAsync(cancellationToken);

    public async Task WriteModeAsync(
        byte address,
        byte network,
        RfMatcherMode mode,
        CancellationToken cancellationToken = default)
    {
        await transactionGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureConnected();

            var modeResponse = await ExchangeCoreAsync(
                    MksRfMatcherProtocol.CreateSetModeRequest(
                        address,
                        network,
                        mode),
                    address,
                    MksRfMatcherProtocol.SetControlModeCommand,
                    cancellationToken)
                .ConfigureAwait(false);

            MksRfMatcherProtocol.ValidateCommandResponse(modeResponse);

            var moveResponse = await ExchangeCoreAsync(
                    MksRfMatcherProtocol.CreateMotorMoveRequest(
                        address,
                        network,
                        true),
                    address,
                    MksRfMatcherProtocol.EnableMotorMoveCommand,
                    cancellationToken)
                .ConfigureAwait(false);

            MksRfMatcherProtocol.ValidateCommandResponse(moveResponse);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    public async Task WriteManualPositionsAsync(
        byte address,
        byte network,
        double tunePosition,
        double loadPosition,
        CancellationToken cancellationToken = default)
    {
        await transactionGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureConnected();

            var loadResponse = await ExchangeCoreAsync(
                    MksRfMatcherProtocol.CreatePositionRequest(
                        address,
                        network,
                        MksRfMatcherProtocol.SetLoadPositionCommand,
                        loadPosition),
                    address,
                    MksRfMatcherProtocol.SetLoadPositionCommand,
                    cancellationToken)
                .ConfigureAwait(false);

            MksRfMatcherProtocol.ValidateCommandResponse(loadResponse);

            var tuneResponse = await ExchangeCoreAsync(
                    MksRfMatcherProtocol.CreatePositionRequest(
                        address,
                        network,
                        MksRfMatcherProtocol.SetTunePositionCommand,
                        tunePosition),
                    address,
                    MksRfMatcherProtocol.SetTunePositionCommand,
                    cancellationToken)
                .ConfigureAwait(false);

            MksRfMatcherProtocol.ValidateCommandResponse(tuneResponse);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    public async Task StopMotionAsync(
        byte address,
        byte network,
        CancellationToken cancellationToken = default)
    {
        var response = await ExchangeAsync(
                MksRfMatcherProtocol.CreateMotorMoveRequest(
                    address,
                    network,
                    false),
                address,
                MksRfMatcherProtocol.EnableMotorMoveCommand,
                cancellationToken)
            .ConfigureAwait(false);

        MksRfMatcherProtocol.ValidateCommandResponse(response);
    }

    public async Task<MksRfMatcherStatus> ReadStatusAsync(
        byte address,
        byte network,
        CancellationToken cancellationToken = default)
    {
        var response = await ExchangeAsync(
                MksRfMatcherProtocol.CreateStatusRequest(address),
                address,
                MksRfMatcherProtocol.QueryStatusCommand,
                cancellationToken)
            .ConfigureAwait(false);

        return MksRfMatcherProtocol.ParseStatus(response, network);
    }

    private async Task<byte[]> ExchangeAsync(
        byte[] request,
        byte address,
        byte command,
        CancellationToken cancellationToken)
    {
        await transactionGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureConnected();

            return await ExchangeCoreAsync(
                    request,
                    address,
                    command,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            transactionGate.Release();
        }
    }

    private async Task<byte[]> ExchangeCoreAsync(
        byte[] request,
        byte address,
        byte command,
        CancellationToken cancellationToken)
    {
        await transport
            .WriteAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var frame = await ReadFrameAsync(cancellationToken)
            .ConfigureAwait(false);

        var data = MksRfMatcherProtocol.ValidateAndGetData(
            frame,
            address,
            command);

        await transport
            .WriteAsync(
                new byte[] { MksRfMatcherProtocol.Acknowledge },
                cancellationToken)
            .ConfigureAwait(false);

        return data;
    }

    private async Task<byte[]> ReadFrameAsync(
        CancellationToken cancellationToken)
    {
        var header = await ReadByteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (header == MksRfMatcherProtocol.Acknowledge)
        {
            header = await ReadByteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var command = await ReadByteAsync(cancellationToken)
            .ConfigureAwait(false);

        var shortLength = header & 0x07;
        var frame = new List<byte> { header, command };
        int dataLength;

        if (shortLength == 7)
        {
            var extendedLength = await ReadByteAsync(cancellationToken)
                .ConfigureAwait(false);

            frame.Add(extendedLength);
            dataLength = extendedLength;
        }
        else
        {
            dataLength = shortLength;
        }

        var remainder = new byte[dataLength + 1];
        await ReadExactlyAsync(remainder, cancellationToken)
            .ConfigureAwait(false);

        frame.AddRange(remainder);
        return frame.ToArray();
    }

    private async Task<byte> ReadByteAsync(
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(buffer, cancellationToken)
            .ConfigureAwait(false);
        return buffer[0];
    }

    private async Task ReadExactlyAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await transport
                .ReadAsync(buffer[total..], cancellationToken)
                .ConfigureAwait(false);

            if (read <= 0)
            {
                throw new EndOfStreamException(
                    "MKS RF matcher transport closed before a complete response was received.");
            }

            total += read;
        }
    }

    private void EnsureConnected()
    {
        if (!transport.IsConnected)
        {
            throw new InvalidOperationException(
                "MKS RF matcher transport is not connected.");
        }
    }
}
