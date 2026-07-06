using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;
using Kwy.Device.MotionCards.Simulation;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class AxisMotionExecutorTests
{
    private static readonly MotionProfile FastProfile = new(500, 10_000, 10_000);

    [Fact]
    public async Task MoveAbsAsync_CompletesFromSharedStateMonitor()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();

        MotionCompletionResult result = await fixture.Executor.MoveAbsAsync(
            1,
            25,
            FastProfile,
            CreateOptions());

        Assert.Equal(25, result.ActualPosition, 3);
    }

    [Fact]
    public async Task MoveAbsAsync_EnforcesSingleFlightPerAxis()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();

        Task<MotionCompletionResult> first = fixture.Executor.MoveAbsAsync(
            1,
            100,
            new MotionProfile(5, 100, 100),
            CreateOptions());

        await Assert.ThrowsAsync<MotionOperationInProgressException>(() =>
            fixture.Executor.MoveAbsAsync(1, 50, FastProfile, CreateOptions()));

        fixture.Card.Stop(1);
        await Assert.ThrowsAsync<MotionPositionException>(() => first);
    }

    [Fact]
    public async Task MoveAbsAsync_ReportsLimitAlarmAndServoDrop()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();

        Task<MotionCompletionResult> limited = fixture.Executor.MoveAbsAsync(
            1, 100, new MotionProfile(5, 100, 100), CreateOptions());
        await Task.Delay(30);
        fixture.Card.SetLimit(1, positive: true, negative: false);
        await Assert.ThrowsAsync<MotionLimitException>(() => limited);

        fixture.Card.SetLimit(1, positive: false, negative: false);
        await WaitUntilAsync(() => !fixture.Monitor.GetAxisSnapshot(1).IsPositiveLimit);
        Task<MotionCompletionResult> alarmed = fixture.Executor.MoveAbsAsync(
            1, 100, new MotionProfile(5, 100, 100), CreateOptions());
        await Task.Delay(30);
        fixture.Card.SetAlarm(1, true);
        await Assert.ThrowsAsync<MotionAlarmException>(() => alarmed);

        fixture.Card.SetAlarm(1, false);
        await WaitUntilAsync(() => !fixture.Monitor.GetAxisSnapshot(1).IsAlarm);
        Task<MotionCompletionResult> disabled = fixture.Executor.MoveAbsAsync(
            1, 100, new MotionProfile(5, 100, 100), CreateOptions());
        await Task.Delay(30);
        fixture.Card.ServoOff(1);
        await Assert.ThrowsAsync<MotionServoDisabledException>(() => disabled);
    }

    [Fact]
    public async Task MoveAbsAsync_StopsWhenCanceled()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();

        Task<MotionCompletionResult> operation = fixture.Executor.MoveAbsAsync(
            1,
            100,
            new MotionProfile(5, 100, 100),
            CreateOptions(),
            cancellation.Token);
        await Task.Delay(30);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await Task.Delay(20);
        Assert.False(fixture.Card.IsMoving(1));
    }

    [Fact]
    public async Task WaitForPositionCrossedAsync_CompletesBeforeFinalPosition()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();

        Task<MotionCompletionResult> motion = fixture.Executor.MoveAbsAsync(
            1,
            100,
            new MotionProfile(100, 1_000, 1_000),
            new MotionExecutionOptions
            {
                PositionTolerance = 0.001,
                Timeout = TimeSpan.FromSeconds(5),
                StartDetectionDelay = TimeSpan.FromMilliseconds(20)
            });
        MotionAxisSnapshot crossing = await fixture.Executor.WaitForPositionCrossedAsync(
            1,
            50,
            PositionCrossingDirection.Positive,
            TimeSpan.FromSeconds(2));

        Assert.True(crossing.Position >= 50);
        await motion;
    }

    [Fact]
    public async Task SeekSensorAsync_SupportsSoftwareAndHardwareStopModes()
    {
        await using ExecutorFixture fixture = await ExecutorFixture.CreateAsync();
        var io = new TestIoCard();

        Task<SensorSeekResult> softwareSeek = fixture.Executor.SeekSensorAsync(
            1,
            io,
            0,
            velocity: 20,
            new SensorSeekOptions
            {
                StopMode = SensorStopMode.SoftwareStop,
                PollInterval = TimeSpan.FromMilliseconds(2),
                Timeout = TimeSpan.FromSeconds(2)
            });
        await Task.Delay(30);
        io.SetInput(0, true, raiseInterrupt: false);
        SensorSeekResult softwareResult = await softwareSeek;
        Assert.Equal(SensorStopMode.SoftwareStop, softwareResult.StopMode);

        io.SetInput(0, false, raiseInterrupt: false);
        Task<SensorSeekResult> hardwareSeek = fixture.Executor.SeekSensorAsync(
            1,
            io,
            0,
            velocity: -20,
            new SensorSeekOptions
            {
                StopMode = SensorStopMode.ControllerHardwareStop,
                Timeout = TimeSpan.FromSeconds(2)
            });
        await Task.Delay(30);
        fixture.Card.Stop(1); // Simulates the controller's hardware-bound stop input.
        io.SetInput(0, true, raiseInterrupt: true);
        SensorSeekResult hardwareResult = await hardwareSeek;
        Assert.Equal(SensorStopMode.ControllerHardwareStop, hardwareResult.StopMode);
    }

    private static MotionExecutionOptions CreateOptions()
        => new()
        {
            PositionTolerance = 0.001,
            Timeout = TimeSpan.FromSeconds(2),
            StartDetectionDelay = TimeSpan.FromMilliseconds(20)
        };

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
        {
            await Task.Delay(2, timeout.Token);
        }
    }

    private sealed class ExecutorFixture : IAsyncDisposable
    {
        private ExecutorFixture(
            SimulationMotionCardDevice card,
            MotionStateMonitor monitor,
            AxisMotionExecutor executor)
        {
            Card = card;
            Monitor = monitor;
            Executor = executor;
        }

        public SimulationMotionCardDevice Card { get; }

        public MotionStateMonitor Monitor { get; }

        public AxisMotionExecutor Executor { get; }

        public static async Task<ExecutorFixture> CreateAsync()
        {
            var card = new SimulationMotionCardDevice(new SimulationMotionCardConfig
            {
                AxisCount = 1,
                UpdateInterval = TimeSpan.FromMilliseconds(2),
                SimulationSpeedRatio = 10
            });
            await card.ConnectAsync();
            card.ServoOn(1);

            var monitor = new MotionStateMonitor(card, new MotionStateMonitorOptions
            {
                AxisCount = 1,
                PollInterval = TimeSpan.FromMilliseconds(2)
            });
            await monitor.StartAsync();
            var safety = new MotionSafetyGuard(card, monitor, new MotionSafetyOptions
            {
                RequireHomedForPositioning = false,
                MaximumSnapshotAge = TimeSpan.FromSeconds(1)
            });
            return new(card, monitor, new AxisMotionExecutor(card, card, monitor, safety));
        }

        public async ValueTask DisposeAsync()
        {
            Executor.Dispose();
            await Monitor.DisposeAsync();
            await Card.DisposeAsync();
        }
    }

    private sealed class TestIoCard : IIoCardDevice, IHardwareInterruptSource
    {
        private ulong inputs;

        public string DeviceId => "TestIo";
        public string DeviceName => "Test IO";
        public ConnectionState State => ConnectionState.Connected;
        public bool IsConnected => true;
        public IDeviceConfig DeviceParameter { get; set; } = new TestConfig();
        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }
        public event EventHandler<ulong>? OnHardwareTriggerReceived;

        public bool ReadDiBit(int channel) => (inputs & (1UL << channel)) != 0;
        public bool[] ReadAllDi() => Enumerable.Range(0, 64).Select(ReadDiBit).ToArray();
        public ulong ReadDiPortMask() => inputs;
        public void WriteDoBit(int channel, bool state) => throw new NotSupportedException();
        public void WriteDoPortMask(ulong mask) => throw new NotSupportedException();
        public void WriteDoPortMask(ulong mask, ulong changedMask) => throw new NotSupportedException();
        public bool[] ReadAllDo() => new bool[64];
        public void WritePulse(int channel, int durationMs) => throw new NotSupportedException();
        public void SetDoName(int channel, string name) { }
        public IEnumerable<(int Index, string Name)> GetAllOutputs() => Array.Empty<(int, string)>();
        public void SetDiName(int channel, string name) { }
        public IEnumerable<(int Index, string Name)> GetAllInputs() => Array.Empty<(int, string)>();
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyConfigurationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void SetInput(int channel, bool state, bool raiseInterrupt)
        {
            ulong bit = 1UL << channel;
            inputs = state ? inputs | bit : inputs & ~bit;
            if (raiseInterrupt)
            {
                OnHardwareTriggerReceived?.Invoke(this, inputs);
            }
        }

        private sealed class TestConfig : IDeviceConfig
        {
            public bool Validate() => true;
        }
    }
}
