using System.Text;
using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using KwyPecvd.Device.VacuumPump.PfeifferA100;

namespace KwyPecvd.Tests.Devices;

public sealed class PfeifferA100BusTests
{
    [Fact]
    public async Task WriteRunningAsync_WritesSysOnCommand()
    {
        var transport = new FakeByteTransport(Encoding.ASCII.GetBytes("#000OK\r"));
        var bus = new PfeifferA100Bus(transport);

        await bus.ConnectAsync();
        await bus.WriteRunningAsync("000", running: true);

        Assert.Equal("#000SYSON\r", Encoding.ASCII.GetString(transport.LastWrite));
    }

    [Fact]
    public async Task ReadStatusAsync_ParsesRunningWarningAndAlarmBits()
    {
        var data = new byte[32];
        data[0] = 0x40;
        data[29] = 0x01;
        data[30] = 0x10;

        byte[] response = [(byte)'#', (byte)'0', (byte)'0', (byte)'0', .. data, (byte)'\r'];
        var transport = new FakeByteTransport(response);
        var bus = new PfeifferA100Bus(transport);

        await bus.ConnectAsync();
        PfeifferA100Status status = await bus.ReadStatusAsync("000");

        Assert.Equal("#000STA\r", Encoding.ASCII.GetString(transport.LastWrite));
        Assert.True(status.IsRunning);
        Assert.True(status.HasWarning);
        Assert.True(status.HasAlarm);
        Assert.True(status.IsOverTemperature);
    }

    [Fact]
    public async Task WriteRunningAsync_WhenResponseContainsError_Throws()
    {
        var transport = new FakeByteTransport(Encoding.ASCII.GetBytes("#000ER\r"));
        var bus = new PfeifferA100Bus(transport);

        await bus.ConnectAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.WriteRunningAsync("000", running: false));
    }

    private sealed class FakeByteTransport(byte[] response) : IByteStreamClient
    {
        private int readOffset;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;
        public byte[] LastWrite { get; private set; } = [];

        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Connected;
            return Task.CompletedTask;
        }

        public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);
            return IsConnected;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            return Task.CompletedTask;
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
