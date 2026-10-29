using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using KwyPecvd.Device.Thermal.TemperatureController.Omron;

namespace KwyPecvd.Tests.Devices;

public sealed class OmronEj1BusTests
{
    [Fact]
    public async Task ReadHoldingRegisterAsync_WritesExpectedFrameAndParsesValue()
    {
        var transport = new FakeByteTransport(
            [0x01, 0x03, 0x02, 0x04, 0xE2, 0x3A, 0xCD]);
        var bus = new OmronEj1Bus(transport);

        await bus.ConnectAsync();
        short value = await bus.ReadHoldingRegisterAsync(0x01, 0x0600);

        Assert.Equal(1_250, value);
        Assert.Equal(
            [0x01, 0x03, 0x06, 0x00, 0x00, 0x01, 0x84, 0x82],
            transport.LastWrite);
    }

    [Fact]
    public async Task WriteSingleRegisterAsync_AcceptsMatchingModbusEcho()
    {
        byte[] echo = [0x01, 0x06, 0x06, 0x40, 0x05, 0xDC, 0x8A, 0x5F];
        var transport = new FakeByteTransport(echo);
        var bus = new OmronEj1Bus(transport);

        await bus.ConnectAsync();
        await bus.WriteSingleRegisterAsync(0x01, 0x0640, 0x05DC);

        Assert.Equal(echo, transport.LastWrite);
    }

    [Fact]
    public async Task ReadHoldingRegisterAsync_ReportsModbusExceptionWithoutWaitingForSevenBytes()
    {
        var transport = new FakeByteTransport(
            [0x01, 0x83, 0x02, 0xC0, 0xF1]);
        var bus = new OmronEj1Bus(transport);

        await bus.ConnectAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.ReadHoldingRegisterAsync(0x01, 0x0600));

        Assert.Contains("Modbus exception 2", exception.Message);
    }

    [Fact]
    public async Task ReadHoldingRegisterAsync_RejectsInvalidCrc()
    {
        var transport = new FakeByteTransport(
            [0x01, 0x03, 0x02, 0x04, 0xE2, 0x00, 0x00]);
        var bus = new OmronEj1Bus(transport);

        await bus.ConnectAsync();

        await Assert.ThrowsAsync<FormatException>(
            () => bus.ReadHoldingRegisterAsync(0x01, 0x0600));
    }

    private sealed class FakeByteTransport(byte[] response) : IByteStreamClient
    {
        private int readOffset;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;
        public byte[] LastWrite { get; private set; } = [];

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            var previous = State;
            State = ConnectionState.Connected;
            ConnectionStateChanged?.Invoke(
                this,
                new ConnectionStateChangedEventArgs(previous, State));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);
            return IsConnected;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            LastWrite = data.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int count = Math.Min(buffer.Length, response.Length - readOffset);
            response.AsMemory(readOffset, count).CopyTo(buffer);
            readOffset += count;
            return ValueTask.FromResult(count);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
