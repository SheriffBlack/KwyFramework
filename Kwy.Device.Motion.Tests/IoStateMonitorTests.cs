using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Core.IO;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class IoStateMonitorTests
{
    [Fact]
    public void Initialize_RejectsUnknownDeviceWithoutReplacingCurrentConfiguration()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        monitor.Initialize([device], [Input("input.ready", "io-1", 0)], []);

        Assert.Throws<ArgumentException>(() =>
            monitor.Initialize([device], [Input("input.invalid", "missing", 1)], []));

        device.SetInput(0, true, raiseInterrupt: true);
        Assert.True(monitor.ReadDi("input.ready"));
    }

    [Fact]
    public void Initialize_RejectsDuplicateStableIdsAcrossInputsAndOutputs()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");

        Assert.Throws<ArgumentException>(() => monitor.Initialize(
            [device],
            [Input("shared.point", "io-1", 0)],
            [Output("shared.point", "io-1", 1)]));
    }

    [Fact]
    public void Initialize_RejectsDuplicateChannelsWithinTheSameSignalDirection()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");

        Assert.Throws<ArgumentException>(() => monitor.Initialize(
            [device],
            [Input("input.first", "io-1", 0), Input("input.second", "IO-1", 0)],
            []));
    }

    [Fact]
    public void Monitor_UsesStableIdForReadsAndEvents()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        string? changedId = null;
        monitor.OnIoStateChanged += (id, _) => changedId = id;
        monitor.Initialize([device], [Input("input.ready", "io-1", 0) with { Name = "可修改显示名" }], []);

        device.SetInput(0, true, raiseInterrupt: true);

        Assert.True(monitor.ReadDi("input.ready"));
        Assert.Equal("input.ready", changedId);
    }

    [Fact]
    public void ReadDi_DistinguishesUnknownPointFromMissingSnapshot()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1") { ThrowOnPortRead = true };
        monitor.Initialize([device], [Input("input.ready", "io-1", 0)], []);

        Assert.Throws<KeyNotFoundException>(() => monitor.ReadDi("input.unknown"));
        Assert.False(monitor.TryReadDi("input.ready", out _));
    }

    [Fact]
    public async Task HardwareInterruptWait_PreservesCancellationWhenCurrentLevelMatches()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        monitor.Initialize([device], [Input("input.ready", "io-1", 0)], []);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task wait = monitor.WaitForHardwareInterruptAsync("input.ready", false, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public void StateNotification_IsolatesFailingSubscribers()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        bool secondSubscriberCalled = false;
        Exception? callbackFailure = null;
        monitor.OnIoStateChanged += (_, _) => throw new InvalidOperationException("Subscriber failed.");
        monitor.OnIoStateChanged += (_, _) => secondSubscriberCalled = true;
        monitor.OnIoNotificationFailed += (_, exception) => callbackFailure = exception;
        monitor.Initialize([device], [Input("input.ready", "io-1", 0)], []);

        device.SetInput(0, true, raiseInterrupt: true);

        Assert.True(secondSubscriberCalled);
        Assert.IsType<InvalidOperationException>(callbackFailure);
    }

    [Fact]
    public void Initialize_RejectsPointPlacedInTheWrongSignalCollection()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");

        Assert.Throws<ArgumentException>(() => monitor.Initialize(
            [device],
            [Output("valve.open", "io-1", 0)],
            []));
    }

    [Fact]
    public void WriteDo_EnforcesOwnerAndConvertsLogicalPolarity()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        IoPoint output = Output("valve.open", "io-1", 2) with
        {
            Owner = "station.transport",
            Inverted = true
        };
        monitor.Initialize([device], [], [output]);

        Assert.Throws<UnauthorizedAccessException>(() => monitor.WriteDo("valve.open", true));
        Assert.Throws<UnauthorizedAccessException>(() => monitor.WriteDo("valve.open", true, "station.other"));

        monitor.WriteDo("valve.open", true, "STATION.TRANSPORT");
        Assert.False(device.GetPhysicalOutput(2));
    }

    [Fact]
    public void ApplySafeOutputs_AppliesOnlyConfiguredLogicalSafeStates()
    {
        using var monitor = new IoStateMonitor();
        var device = new TestIoCard("io-1");
        monitor.Initialize(
            [device],
            [],
            [
                Output("valve.safe", "io-1", 0) with { Owner = "station-a", SafeState = false, Inverted = true },
                Output("lamp.unmanaged", "io-1", 1)
            ]);
        device.WriteDoBit(1, true);

        monitor.ApplySafeOutputs();

        Assert.True(device.GetPhysicalOutput(0));
        Assert.True(device.GetPhysicalOutput(1));
    }

    private static IoPoint Input(string id, string deviceId, int channel) => new()
    {
        Id = id,
        Name = id,
        DeviceId = deviceId,
        Kind = IoSignalKind.DigitalInput,
        Channel = channel
    };

    private static IoPoint Output(string id, string deviceId, int channel) => new()
    {
        Id = id,
        Name = id,
        DeviceId = deviceId,
        Kind = IoSignalKind.DigitalOutput,
        Channel = channel
    };

    private sealed class TestIoCard(string deviceId) : IIoCardDevice, IHardwareInterruptSource
    {
        private ulong inputs;
        private ulong outputs;

        public bool ThrowOnPortRead { get; init; }

        public string DeviceId { get; } = deviceId;
        public string DeviceName => DeviceId;
        public ConnectionState State => ConnectionState.Connected;
        public bool IsConnected => true;
        public IDeviceConfig DeviceParameter { get; set; } = new TestConfig();
        public int DigitalInputCount => 64;
        public int DigitalOutputCount => 64;
        public event EventHandler<IoSignalSnapshot>? HardwareInterruptReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
        public event EventHandler<DeviceOperationEventArgs>? OperationOccurred { add { } remove { } }

        public bool ReadDiBit(int channel) => (inputs & (1UL << channel)) != 0;
        public bool[] ReadAllDi() => Enumerable.Range(0, 64).Select(ReadDiBit).ToArray();
        public ulong ReadDiPortMask()
            => ThrowOnPortRead ? throw new InvalidOperationException("Test read failure.") : inputs;
        public void WriteDoBit(int channel, bool state)
        {
            ulong bit = 1UL << channel;
            outputs = state ? outputs | bit : outputs & ~bit;
        }
        public void WriteDoPortMask(ulong mask) => outputs = mask;
        public void WriteDoPortMask(ulong mask, ulong changedMask) => outputs = (outputs & ~changedMask) | (mask & changedMask);
        public bool[] ReadAllDo() => Enumerable.Range(0, 64).Select(GetPhysicalOutput).ToArray();
        public void WritePulse(int channel, int durationMs) => WriteDoBit(channel, true);
        public void SetDoName(int channel, string name) { }
        public IEnumerable<(int Index, string Name)> GetAllOutputs() => [];
        public void SetDiName(int channel, string name) { }
        public IEnumerable<(int Index, string Name)> GetAllInputs() => [];
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public bool GetPhysicalOutput(int channel) => (outputs & (1UL << channel)) != 0;

        public void SetInput(int channel, bool state, bool raiseInterrupt = false)
        {
            ulong bit = 1UL << channel;
            inputs = state ? inputs | bit : inputs & ~bit;
            if (raiseInterrupt)
                HardwareInterruptReceived?.Invoke(this, new IoSignalSnapshot(DeviceId, inputs, DateTimeOffset.UtcNow, IoSnapshotSource.HardwareInterrupt));
        }

        private sealed class TestConfig : IDeviceConfig
        {
            public bool Validate() => true;
        }
    }
}
