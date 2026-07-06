using System.Diagnostics;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;
using Kwy.Device.Core.Motion;

const int operationCount = 50_000;
var monitor = new BenchmarkStateMonitor();
var card = new BenchmarkMotionCard(monitor);
var safety = new MotionSafetyGuard(card, monitor, new MotionSafetyOptions
{
    RequireHomedForPositioning = false,
    MaximumSnapshotAge = TimeSpan.FromMinutes(1)
});
using var executor = new AxisMotionExecutor(card, card, monitor, safety);
var profile = new MotionProfile(100, 1_000, 1_000);
var options = new MotionExecutionOptions
{
    PositionTolerance = 0.001,
    Timeout = TimeSpan.FromSeconds(1)
};

for (int i = 0; i < 1_000; i++)
{
    await executor.MoveAbsAsync(1, (i & 1) == 0 ? 1 : 0, profile, options);
}

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
int gen0Before = GC.CollectionCount(0);
int gen1Before = GC.CollectionCount(1);
int gen2Before = GC.CollectionCount(2);
var stopwatch = Stopwatch.StartNew();

for (int i = 0; i < operationCount; i++)
{
    await executor.MoveAbsAsync(1, (i & 1) == 0 ? 1 : 0, profile, options);
}

stopwatch.Stop();
long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
Console.WriteLine($"Operations: {operationCount:N0}");
Console.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalMilliseconds:N1} ms");
Console.WriteLine($"Allocated: {allocated:N0} bytes");
Console.WriteLine($"Allocated/operation: {(double)allocated / operationCount:N1} bytes");
Console.WriteLine($"Gen0 collections: {GC.CollectionCount(0) - gen0Before}");
Console.WriteLine($"Gen1 collections: {GC.CollectionCount(1) - gen1Before}");
Console.WriteLine($"Gen2 collections: {GC.CollectionCount(2) - gen2Before}");

internal sealed class BenchmarkMotionCard : IMotionCard, IAxisMotionController, IMotionProfileController
{
    private readonly BenchmarkStateMonitor monitor;

    public BenchmarkMotionCard(BenchmarkStateMonitor monitor)
    {
        this.monitor = monitor;
    }

    public string DeviceId => "Benchmark";
    public string DeviceName => "Benchmark Motion";
    public bool IsConnected => true;
    public ConnectionState State => ConnectionState.Connected;
    public IDeviceConfig DeviceParameter { get; set; } = new BenchmarkConfig();
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged { add { } remove { } }
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred { add { } remove { } }
    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ApplyConfigurationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void ServoOn(short axis) { }
    public void ServoOff(short axis) { }
    public void ClearError(short axis) { }

    public void MoveAbs(short axis, double position, double velocity, double acc = 0.5, double dec = 0.5)
        => MoveAbs(axis, position, new MotionProfile(velocity, acc, dec));

    public void MoveRel(short axis, double distance, double velocity, double acc = 0.5, double dec = 0.5)
        => MoveAbs(axis, monitor.GetAxisSnapshot(axis).Position + distance, velocity, acc, dec);

    public void MoveAbs(short axis, double position, MotionProfile profile)
    {
        MotionAxisSnapshot current = monitor.GetAxisSnapshot(axis);
        monitor.Publish(CreateSnapshot(current.Position, moving: true));
        monitor.Publish(CreateSnapshot(position, moving: false));
    }

    public void MoveRel(short axis, double distance, MotionProfile profile)
        => MoveAbs(axis, monitor.GetAxisSnapshot(axis).Position + distance, profile);

    public void MoveJog(short axis, double velocity) { }
    public void Stop(short axis) { }
    public void Abort(short axis) { }
    public void GoHome(short axis) { }
    public void SetSoftLimit(short axis, double positive, double negative) { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static MotionAxisSnapshot CreateSnapshot(double position, bool moving)
        => new(1, position, position, moving ? 1 : 0, 0, moving, false, false, false,
            DateTimeOffset.Now, isServoEnabled: true, HomeState.Succeeded);

    private sealed class BenchmarkConfig : IDeviceConfig
    {
        public bool Validate() => true;
    }
}

internal sealed class BenchmarkStateMonitor : IMotionStateMonitor
{
    private MotionAxisSnapshot snapshot = new(
        1, 0, 0, 0, 0, false, false, false, false, DateTimeOffset.Now, true, HomeState.Succeeded);

    public bool IsRunning => true;
    public event Action<MotionAxisSnapshot>? AxisSnapshotCaptured;
    public event EventHandler<MotionAxisSnapshotChangedEventArgs>? AxisSnapshotChanged;
    public event EventHandler<ErrorOccurredEventArgs>? MonitorErrorOccurred;
    public MotionAxisSnapshot GetAxisSnapshot(short axis) => snapshot;
    public IReadOnlyDictionary<short, MotionAxisSnapshot> GetAllAxisSnapshots()
        => new Dictionary<short, MotionAxisSnapshot> { [1] = snapshot };
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Publish(MotionAxisSnapshot value)
    {
        MotionAxisSnapshot previous = snapshot;
        snapshot = value;
        AxisSnapshotCaptured?.Invoke(value);
        AxisSnapshotChanged?.Invoke(this, new(value, previous));
    }
}
