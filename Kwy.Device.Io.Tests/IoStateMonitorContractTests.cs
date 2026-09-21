using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Core.IO;
using Xunit;

namespace Kwy.Device.Io.Tests;

public sealed class IoStateMonitorContractTests
{
    [Fact]
    public void Initialize_RejectsPointOutsideDeviceCapabilities()
    {
        using var monitor = new IoStateMonitor();
        var device = new FakeIoCard(inputCount: 2, outputCount: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Initialize(
            [device],
            [Input("sensor.invalid", 2)],
            []));
    }

    [Fact]
    public void IoCardBase_UsesDeclaredCapabilitiesForMaskAndPointRegistry()
    {
        var card = new BaseCard();
        card.WriteDoPortMask(mask: 0b11, changedMask: ulong.MaxValue);
        card.SetDoName(1, "Valve");

        Assert.True(card.GetOutput(0));
        Assert.True(card.GetOutput(1));
        Assert.Single(card.GetAllOutputs());
        Assert.Throws<ArgumentOutOfRangeException>(() => card.SetDoName(2, "Invalid"));
    }

    [Fact]
    public async Task WritePulse_ReplacesEarlierPulseForTheSameLogicalOutput()
    {
        using var monitor = new IoStateMonitor();
        var device = new FakeIoCard();
        monitor.Initialize([device], [], [Output("valve.open", 0)]);

        monitor.WritePulse("valve.open", 60);
        await Task.Delay(25);
        monitor.WritePulse("valve.open", 60);
        await Task.Delay(45);
        Assert.True(device.GetOutput(0));

        await Task.Delay(35);
        Assert.False(device.GetOutput(0));
    }

    [Fact]
    public void SafeOutputFailure_IsReportedAsAggregateFailure()
    {
        using var monitor = new IoStateMonitor();
        var device = new FakeIoCard { ThrowOnWrite = true };
        monitor.Initialize([device], [], [Output("valve.safe", 0) with { SafeState = false }]);

        Assert.Throws<AggregateException>(monitor.ApplySafeOutputs);
    }

    [Fact]
    public void ReadFailureSubscriber_DoesNotStopOtherSubscribers()
    {
        using var monitor = new IoStateMonitor();
        var device = new FakeIoCard { ThrowOnRead = true };
        using var observed = new ManualResetEventSlim();
        monitor.OnIoReadFailed += (_, _) => throw new InvalidOperationException("Test subscriber failure.");
        monitor.OnIoReadFailed += (_, _) => observed.Set();
        monitor.Initialize([device], [Input("sensor.ready", 0)], []);

        Assert.True(observed.Wait(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void HardwareSnapshot_ContainsSourceAndTimestamp()
    {
        using var monitor = new IoStateMonitor();
        var device = new FakeIoCard();
        IoSignalSnapshot? received = null;
        monitor.OnIoSnapshotReceived += snapshot =>
        {
            if (snapshot.Source == IoSnapshotSource.HardwareInterrupt)
                received = snapshot;
        };
        monitor.Initialize([device], [Input("sensor.ready", 0)], []);

        device.RaiseInterrupt(1, IoTriggerEdge.Rising);

        Assert.NotNull(received);
        Assert.Equal(device.DeviceId, received!.DeviceId);
        Assert.Equal(IoTriggerEdge.Rising, received.TriggerEdge);
        Assert.NotEqual(default, received.Timestamp);
    }

    private static IoPoint Input(string id, int channel) => new()
    {
        Id = id, Name = id, DeviceId = "io-1", Kind = IoSignalKind.DigitalInput, Channel = channel
    };

    private static IoPoint Output(string id, int channel) => new()
    {
        Id = id, Name = id, DeviceId = "io-1", Kind = IoSignalKind.DigitalOutput, Channel = channel
    };

    private sealed class FakeIoCard(int inputCount = 8, int outputCount = 8) : IIoCardDevice, IHardwareInterruptSource
    {
        private ulong inputs;
        private ulong outputs;
        public string DeviceId => "io-1";
        public string DeviceName => "Fake IO";
        public bool IsConnected => true;
        public ConnectionState State => ConnectionState.Connected;
        public IDeviceConfig DeviceParameter { get; set; } = new FakeConfig();
        public int DigitalInputCount => inputCount;
        public int DigitalOutputCount => outputCount;
        public bool ThrowOnRead { get; init; }
        public bool ThrowOnWrite { get; init; }
        public event EventHandler<IoSignalSnapshot>? HardwareInterruptReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
        public event EventHandler<DeviceOperationEventArgs>? OperationOccurred { add { } remove { } }
        public bool ReadDiBit(int channel) => (inputs & (1UL << channel)) != 0;
        public bool[] ReadAllDi() => Enumerable.Range(0, DigitalInputCount).Select(ReadDiBit).ToArray();
        public ulong ReadDiPortMask() => ThrowOnRead ? throw new InvalidOperationException("Read failed.") : inputs;
        public void WriteDoBit(int channel, bool state)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("Write failed.");
            ulong bit = 1UL << channel;
            outputs = state ? outputs | bit : outputs & ~bit;
        }
        public void WriteDoPortMask(ulong mask) => outputs = mask;
        public void WriteDoPortMask(ulong mask, ulong changedMask) => outputs = (outputs & ~changedMask) | (mask & changedMask);
        public bool[] ReadAllDo() => Enumerable.Range(0, DigitalOutputCount).Select(GetOutput).ToArray();
        public void WritePulse(int channel, int durationMs) => WriteDoBit(channel, true);
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public bool GetOutput(int channel) => (outputs & (1UL << channel)) != 0;
        public void RaiseInterrupt(ulong mask, IoTriggerEdge edge)
        {
            inputs = mask;
            HardwareInterruptReceived?.Invoke(this, new IoSignalSnapshot(DeviceId, mask, DateTimeOffset.UtcNow, IoSnapshotSource.HardwareInterrupt, edge));
        }
        private sealed class FakeConfig : IDeviceConfig { public bool Validate() => true; }
    }

    private sealed class BaseCard : IoCardBase
    {
        private ulong outputs;

        public BaseCard() : base("base-io", "Base IO", new BaseConfig()) { }
        public override string DeviceModel => "Test";
        public bool GetOutput(int channel) => (outputs & (1UL << channel)) != 0;
        public override void WriteDoBit(int channel, bool state)
        {
            ulong bit = 1UL << channel;
            outputs = state ? outputs | bit : outputs & ~bit;
        }
        public override bool ReadDiBit(int channel) => false;
        public override bool[] ReadAllDi() => new bool[2];
        public override bool[] ReadAllDo() => Enumerable.Range(0, 2).Select(GetOutput).ToArray();
        public override ulong ReadDiPortMask() => 0;
        protected override int GetDigitalInputChannelCount() => 2;
        protected override int GetDigitalOutputChannelCount() => 2;
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override bool IsConnectionAlive() => true;
        private sealed class BaseConfig : IDeviceConfig { public bool Validate() => true; }
    }
}
