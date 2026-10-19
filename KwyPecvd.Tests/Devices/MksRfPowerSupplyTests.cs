using System.Text;
using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using KwyPecvd.Device.RfPowerSupply;
using KwyPecvd.Device.RfPowerSupply.Mks;

namespace KwyPecvd.Tests.Devices;

public sealed class MksRfPowerSupplyTests
{
    [Fact]
    public async Task Commands_WriteImmediatelyAndCyclesOnlyReadStatus()
    {
        var bus = new FakeMksBus();
        var powerSupply = CreatePowerSupply(bus);

        await powerSupply.InitializeAsync();
        await powerSupply.ExecuteCycleAsync();
        int writesAfterInitialize = bus.WriteCount;

        await powerSupply.SetPowerAsync(500);
        var result = await powerSupply.EnableAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(500, bus.LastPower);
        Assert.True(bus.LastEnabled);
        Assert.Equal(writesAfterInitialize + 2, bus.WriteCount);

        await powerSupply.ExecuteCycleAsync();
        Assert.Equal(writesAfterInitialize + 2, bus.WriteCount);
    }

    [Fact]
    public async Task ExecuteCycleAsync_WhenReflectedPowerIsHigh_DisablesOutputImmediately()
    {
        var bus = new FakeMksBus();
        var powerSupply = CreatePowerSupply(bus);

        await powerSupply.InitializeAsync();
        await powerSupply.ExecuteCycleAsync();
        await powerSupply.SetPowerAsync(500);
        await powerSupply.EnableAsync();
        bus.ReflectedPower = 250;

        await powerSupply.ExecuteCycleAsync();

        Assert.False(bus.LastEnabled);
        Assert.False(powerSupply.Snapshot.CommandedEnabled);
        Assert.True(powerSupply.Snapshot.IsReflectedPowerHigh);
    }

    [Fact]
    public async Task Bus_UsesMigratedMksCommandsAndParsesActualPower()
    {
        var transport = new FakeByteTransport(Encoding.ASCII.GetBytes("500 480 12 320 13560000\r"));
        var bus = new MksRfPowerBus(transport);

        await bus.ConnectAsync();
        MksRfPowerStatus status = await bus.ReadStatusAsync();

        Assert.Equal("ACT\r", Encoding.ASCII.GetString(transport.LastWrite));
        Assert.Equal(500, status.SetPoint);
        Assert.Equal(480, status.ForwardPower);
        Assert.Equal(12, status.ReflectedPower);
        Assert.Equal(13_560_000, status.Frequency);
    }

    [Theory]
    [InlineData(true, "TRG\r")]
    [InlineData(false, "OFF\r")]
    public async Task Bus_WritesExpectedEnableCommand(bool enabled, string expected)
    {
        var transport = new FakeByteTransport(Encoding.ASCII.GetBytes("OK\r"));
        var bus = new MksRfPowerBus(transport);

        await bus.ConnectAsync();
        await bus.WriteEnabledAsync(enabled);

        Assert.Equal(expected, Encoding.ASCII.GetString(transport.LastWrite));
    }

    private static MksRfPowerSupply CreatePowerSupply(IMksRfPowerBus bus) =>
        new(
            new MksRfPowerSupplyDefinition
            {
                PowerSupply = new RfPowerSupplyDefinition
                {
                    Id = "RF_PM1",
                    ModuleId = "PM1",
                    DisplayName = "PM1 MKS RF",
                    FrequencyHz = 13_560_000,
                    MaximumPower = 2_000,
                    ReflectedPowerLimit = 200,
                    Tolerance = 20,
                    ReflectedPowerDelay = TimeSpan.Zero
                }
            },
            bus);

    private sealed class FakeMksBus : IMksRfPowerBus
    {
        public bool IsConnected { get; private set; }
        public double LastPower { get; private set; }
        public bool LastEnabled { get; private set; }
        public int WriteCount { get; private set; }
        public double ReflectedPower { get; set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WritePowerAsync(double power, CancellationToken cancellationToken = default)
        {
            LastPower = power;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            LastEnabled = enabled;
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task<MksRfPowerStatus> ReadStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new MksRfPowerStatus(
                SetPoint: LastPower,
                ForwardPower: LastEnabled ? LastPower : 0,
                ReflectedPower: ReflectedPower,
                PaVoltage: 300,
                Frequency: 13_560_000));
    }

    private sealed class FakeByteTransport(byte[] response) : IByteTransport
    {
        private int offset;
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

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            LastWrite = data.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = Math.Min(buffer.Length, response.Length - offset);
            response.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return ValueTask.FromResult(count);
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
