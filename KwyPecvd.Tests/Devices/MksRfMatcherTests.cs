using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using KwyPecvd.Device.RfMatcher;
using KwyPecvd.Device.RfMatcher.Mks;

namespace KwyPecvd.Tests.Devices;

public sealed class MksRfMatcherTests
{
    [Fact]
    public async Task Commands_WriteImmediatelyAndCyclesOnlyReadStatus()
    {
        var bus = new FakeMksRfMatcherBus();
        var matcher = CreateMatcher(bus);

        await matcher.InitializeAsync();
        await matcher.ExecuteCycleAsync();

        var writesBeforeCommands = bus.WriteCount;

        await matcher.SetModeAsync(RfMatcherMode.Manual);
        var result = await matcher.SetManualPositionAsync(25, 75);

        Assert.True(result.Succeeded);
        Assert.Equal(RfMatcherMode.Manual, bus.Mode);
        Assert.Equal(25, bus.TunePosition);
        Assert.Equal(75, bus.LoadPosition);
        Assert.Equal(writesBeforeCommands + 2, bus.WriteCount);

        await matcher.ExecuteCycleAsync();

        Assert.Equal(writesBeforeCommands + 2, bus.WriteCount);
        Assert.Equal(25, matcher.Snapshot.TunePosition);
        Assert.Equal(75, matcher.Snapshot.LoadPosition);
    }

    [Fact]
    public async Task Bus_ParsesNetworkTwoStatusAndAcknowledgesResponse()
    {
        const byte address = 2;
        var statusData = new byte[88];

        statusData[0] = 0b0000_1100;
        statusData[1] = 0b0100_0000;
        WritePosition(statusData, 12, 75);
        WritePosition(statusData, 14, 25);

        var transport = new FakeByteTransport(
            CreateResponse(
                address,
                command: 219,
                statusData));

        var bus = new MksRfMatcherBus(transport);

        await bus.ConnectAsync();
        var status = await bus.ReadStatusAsync(address, network: 2);

        Assert.Equal(new byte[] { 0x10, 219, 203 }, transport.Writes[0]);
        Assert.Equal(new byte[] { 0x06 }, transport.Writes[1]);
        Assert.Equal(RfMatcherMode.Auto, status.Mode);
        Assert.Equal(25, status.TunePosition);
        Assert.Equal(75, status.LoadPosition);
        Assert.True(status.IsOutputOn);
        Assert.True(status.IsMatched);
        Assert.False(status.IsFaulted);
    }

    private static MksRfMatcher CreateMatcher(IMksRfMatcherBus bus) =>
        new(
            new MksRfMatcherDefinition
            {
                Matcher = new RfMatcherDefinition
                {
                    Id = "MATCH_PM1",
                    ModuleId = "PM1",
                    DisplayName = "PM1 MKS Matcher"
                },
                Address = 2,
                Network = 1
            },
            bus);

    private static void WritePosition(
        byte[] data,
        int offset,
        double position)
    {
        var raw = (ushort)(position * 100);
        data[offset] = (byte)raw;
        data[offset + 1] = (byte)(raw >> 8);
    }

    private static byte[] CreateResponse(
        byte address,
        byte command,
        byte[] data)
    {
        var frame = new byte[data.Length + 4];
        frame[0] = (byte)((address << 3) | 7);
        frame[1] = command;
        frame[2] = (byte)data.Length;
        data.CopyTo(frame, 3);

        byte checksum = 0;

        for (var index = 0; index < frame.Length - 1; index++)
        {
            checksum ^= frame[index];
        }

        frame[^1] = checksum;
        return [0x06, .. frame];
    }

    private sealed class FakeMksRfMatcherBus : IMksRfMatcherBus
    {
        public bool IsConnected { get; private set; }

        public RfMatcherMode Mode { get; private set; } =
            RfMatcherMode.Unknown;

        public double TunePosition { get; private set; }

        public double LoadPosition { get; private set; }

        public int WriteCount { get; private set; }

        public Task ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteModeAsync(
            byte address,
            byte network,
            RfMatcherMode mode,
            CancellationToken cancellationToken = default)
        {
            Mode = mode;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteManualPositionsAsync(
            byte address,
            byte network,
            double tunePosition,
            double loadPosition,
            CancellationToken cancellationToken = default)
        {
            TunePosition = tunePosition;
            LoadPosition = loadPosition;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task StopMotionAsync(
            byte address,
            byte network,
            CancellationToken cancellationToken = default)
        {
            Mode = RfMatcherMode.Unknown;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task<MksRfMatcherStatus> ReadStatusAsync(
            byte address,
            byte network,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new MksRfMatcherStatus(
                    Mode,
                    TunePosition,
                    LoadPosition,
                    IsOutputOn: false,
                    IsMatched: false,
                    IsFaulted: false));
    }

    private sealed class FakeByteTransport(byte[] response) : IByteTransport
    {
        private int offset;

        public ConnectionState State { get; private set; } =
            ConnectionState.Disconnected;

        public bool IsConnected =>
            State == ConnectionState.Connected;

        public List<byte[]> Writes { get; } = [];

        public event EventHandler<ConnectionStateChangedEventArgs>?
            ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ErrorOccurredEventArgs>?
            ErrorOccurred
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(
            CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var count = Math.Min(
                buffer.Length,
                response.Length - offset);

            response.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return ValueTask.FromResult(count);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
