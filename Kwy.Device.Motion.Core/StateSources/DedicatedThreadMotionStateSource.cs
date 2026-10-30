using System.Diagnostics;
using Kwy.Device.Motion.Abstractions;

namespace Kwy.Device.Motion.Core.StateSources;

/// <summary>
/// 使用专用后台线程周期性批量读取轴状态。
/// 仅用于同步 SDK 阻塞、轮询频率较高或需要隔离线程池抖动的设备。
/// </summary>
public sealed class DedicatedThreadMotionStateSource : MotionStateSourceBase
{
    private readonly AxisSnapshotCaptureBuffer captureBuffer;
    private readonly TimeSpan pollInterval;
    private readonly string threadName;
    private CancellationTokenSource? loopCts;
    private Thread? loopThread;

    public DedicatedThreadMotionStateSource(
        IAxisSnapshotReader snapshotReader,
        MotionStateMonitorOptions options,
        string? threadName = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        pollInterval = options.PollInterval;
        captureBuffer = new AxisSnapshotCaptureBuffer(snapshotReader, options.GetAxes());
        this.threadName = string.IsNullOrWhiteSpace(threadName) ? "Kwy.Motion.State" : threadName;
    }

    public override MotionStateAcquisitionMode AcquisitionMode => MotionStateAcquisitionMode.DedicatedThread;

    protected override Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        captureBuffer.Capture(PublishSnapshot);
        loopCts = new CancellationTokenSource();
        loopThread = new Thread(() => Run(loopCts.Token))
        {
            IsBackground = true,
            Name = threadName
        };
        loopThread.Start();
        return Task.CompletedTask;
    }

    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        Thread? thread = loopThread;
        if (thread is null)
        {
            return;
        }

        loopCts?.Cancel();
        await Task.Run(thread.Join).ConfigureAwait(false);
        loopCts?.Dispose();
        loopCts = null;
        loopThread = null;
    }

    private void Run(CancellationToken cancellationToken)
    {
        long intervalTicks = Math.Max(1, (long)(pollInterval.TotalSeconds * Stopwatch.Frequency));
        long nextCapture = Stopwatch.GetTimestamp() + intervalTicks;

        while (!cancellationToken.IsCancellationRequested)
        {
            long remainingTicks = nextCapture - Stopwatch.GetTimestamp();
            if (remainingTicks > 0)
            {
                TimeSpan delay = TimeSpan.FromSeconds((double)remainingTicks / Stopwatch.Frequency);
                if (cancellationToken.WaitHandle.WaitOne(delay))
                {
                    return;
                }
            }

            try
            {
                captureBuffer.Capture(PublishSnapshot);
            }
            catch (Exception exception)
            {
                PublishError(exception, $"Dedicated motion state capture failed: {exception.Message}");
            }

            long now = Stopwatch.GetTimestamp();
            nextCapture += intervalTicks;
            if (nextCapture <= now)
            {
                nextCapture = now + intervalTicks;
            }
        }
    }
}
