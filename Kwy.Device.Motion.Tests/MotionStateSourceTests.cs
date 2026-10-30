using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Core;
using Kwy.Device.Motion.Core.StateSources;
using Xunit;

namespace Kwy.Device.Motion.Tests;

public sealed class MotionStateSourceTests
{
    [Fact]
    public async Task PollingSource_PublishesInitialSnapshotBeforeStartReturns()
    {
        var reader = new RecordingSnapshotReader();
        var options = CreateOptions(MotionStateAcquisitionMode.Polling);
        await using var monitor = new MotionStateMonitor(reader, options);

        await monitor.StartAsync();

        Assert.True(monitor.IsRunning);
        Assert.Equal(MotionStateAcquisitionMode.Polling, monitor.AcquisitionMode);
        Assert.Equal(1, monitor.GetAxisSnapshot(1).Axis);
    }

    [Fact]
    public async Task DedicatedThreadSource_CapturesSubsequentFramesOnDedicatedThread()
    {
        int callerThreadId = Environment.CurrentManagedThreadId;
        var reader = new RecordingSnapshotReader();
        var options = CreateOptions(MotionStateAcquisitionMode.DedicatedThread);
        await using var monitor = new MotionStateMonitor(reader, options);
        var subsequentCapture = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int captures = 0;
        monitor.AxisSnapshotCaptured += _ =>
        {
            if (Interlocked.Increment(ref captures) >= 2)
            {
                subsequentCapture.TrySetResult(Environment.CurrentManagedThreadId);
            }
        };

        await monitor.StartAsync();
        int captureThreadId = await subsequentCapture.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(MotionStateAcquisitionMode.DedicatedThread, monitor.AcquisitionMode);
        Assert.NotEqual(callerThreadId, captureThreadId);
    }

    [Fact]
    public async Task ExplicitCallbackSource_UsesSameMonitorAndSnapshotStore()
    {
        var source = new TestCallbackStateSource(publishInitialSnapshot: true);
        var options = CreateOptions(MotionStateAcquisitionMode.Callback);
        await using var monitor = new MotionStateMonitor(source, options);

        await monitor.StartAsync();
        source.Push(position: 12.5);

        Assert.Equal(MotionStateAcquisitionMode.Callback, monitor.AcquisitionMode);
        Assert.Equal(12.5, monitor.GetAxisSnapshot(1).EncoderPosition);
    }

    [Fact]
    public async Task StateSourceWithoutInitialFrame_IsRejectedAndStopped()
    {
        var source = new TestCallbackStateSource(publishInitialSnapshot: false);
        var options = CreateOptions(MotionStateAcquisitionMode.Callback);
        await using var monitor = new MotionStateMonitor(source, options);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => monitor.StartAsync());

        Assert.Contains("initial snapshot", exception.Message);
        Assert.False(source.IsRunning);
    }

    private static MotionStateMonitorOptions CreateOptions(MotionStateAcquisitionMode mode) => new()
    {
        AcquisitionMode = mode,
        Axes = [1],
        PollInterval = TimeSpan.FromMilliseconds(5)
    };

    private sealed class RecordingSnapshotReader : IAxisSnapshotReader
    {
        private int sequence;

        public MotionAxisSnapshot GetAxisSnapshot(short axis)
        {
            int value = Interlocked.Increment(ref sequence);
            return CreateSnapshot(axis, value);
        }
    }

    private sealed class TestCallbackStateSource(bool publishInitialSnapshot) : MotionStateSourceBase
    {
        public override MotionStateAcquisitionMode AcquisitionMode => MotionStateAcquisitionMode.Callback;

        public void Push(double position) => PublishSnapshot(CreateSnapshot(1, position));

        protected override Task StartCoreAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (publishInitialSnapshot)
            {
                Push(0);
            }

            return Task.CompletedTask;
        }

        protected override Task StopCoreAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static MotionAxisSnapshot CreateSnapshot(short axis, double position) => new(
        axis,
        position,
        position,
        velocity: 0,
        rawStatus: 0,
        isMoving: false,
        isAlarm: false,
        isPositiveLimit: false,
        isNegativeLimit: false,
        DateTimeOffset.UtcNow,
        isServoEnabled: true,
        homeState: HomeState.Succeeded);
}
