using Kwy.Device.Motion.Abstractions;

namespace Kwy.Device.Motion.Core.StateSources;

/// <summary>使用 <see cref="PeriodicTimer"/> 在线程池上周期性批量读取轴状态。</summary>
public sealed class PollingMotionStateSource : MotionStateSourceBase
{
    private readonly AxisSnapshotCaptureBuffer captureBuffer;
    private readonly TimeSpan pollInterval;
    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public PollingMotionStateSource(IAxisSnapshotReader snapshotReader, MotionStateMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        pollInterval = options.PollInterval;
        captureBuffer = new AxisSnapshotCaptureBuffer(snapshotReader, options.GetAxes());
    }

    public override MotionStateAcquisitionMode AcquisitionMode => MotionStateAcquisitionMode.Polling;

    protected override Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        captureBuffer.Capture(PublishSnapshot);
        loopCts = new CancellationTokenSource();
        loopTask = RunAsync(loopCts.Token);
        return Task.CompletedTask;
    }

    protected override async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (loopTask is null)
        {
            return;
        }

        loopCts?.Cancel();
        try
        {
            await loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (loopCts?.IsCancellationRequested == true)
        {
        }
        finally
        {
            loopCts?.Dispose();
            loopCts = null;
            loopTask = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(pollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                captureBuffer.Capture(PublishSnapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                PublishError(exception, $"Motion state polling failed: {exception.Message}");
            }
        }
    }
}
