using System.Collections.Concurrent;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// Polls axis snapshots from a motion card and exposes cached state.
/// </summary>
public sealed class MotionStateMonitor : IMotionStateMonitor
{
    private readonly IAxisSnapshotReader snapshotReader;
    private readonly MotionStateMonitorOptions options;
    private readonly short[] monitoredAxes;
    private readonly MotionAxisSnapshot[] captureBuffer;
    private readonly ConcurrentDictionary<short, MotionAxisSnapshot> snapshots = new();
    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);
    private CancellationTokenSource? monitorCts;
    private Task? monitorTask;
    private bool disposed;

    public MotionStateMonitor(IAxisSnapshotReader snapshotReader, MotionStateMonitorOptions options)
    {
        this.snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        monitoredAxes = this.options.GetAxes().ToArray();
        captureBuffer = new MotionAxisSnapshot[monitoredAxes.Length];
    }

    public bool IsRunning => monitorTask is { IsCompleted: false };

    public event Action<MotionAxisSnapshot>? AxisSnapshotCaptured;

    public event EventHandler<MotionAxisSnapshotChangedEventArgs>? AxisSnapshotChanged;

    public event EventHandler<ErrorOccurredEventArgs>? MonitorErrorOccurred;

    public MotionAxisSnapshot GetAxisSnapshot(short axis)
    {
        ThrowIfDisposed();

        if (snapshots.TryGetValue(axis, out var snapshot))
        {
            return snapshot;
        }

        throw new KeyNotFoundException($"Axis {axis} snapshot has not been captured yet.");
    }

    public IReadOnlyDictionary<short, MotionAxisSnapshot> GetAllAxisSnapshots()
    {
        ThrowIfDisposed();
        return snapshots.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // The monitor loop owns an independent lifetime. The StartAsync token only gates startup.
            monitorCts = new CancellationTokenSource();
            monitorTask = RunAsync(monitorCts.Token);
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (monitorTask is null)
            {
                return;
            }

            monitorCts?.Cancel();

            try
            {
                await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                monitorCts?.Dispose();
                monitorCts = null;
                monitorTask = null;
            }
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        disposed = true;
        lifecycleSemaphore.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        CaptureAllSafely(cancellationToken);

        using var timer = new PeriodicTimer(options.PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            CaptureAllSafely(cancellationToken);
        }
    }

    private void CaptureAllSafely(CancellationToken cancellationToken)
    {
        try
        {
            CaptureAll();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            MonitorErrorOccurred?.Invoke(
                this,
                new ErrorOccurredEventArgs(
                    ex,
                    $"Motion state capture failed: {ex.Message}"));
        }
    }

    private void CaptureAll()
    {
        if (snapshotReader is IBufferedAxisSnapshotReader bufferedReader)
        {
            bufferedReader.GetMultipleAxisSnapshots(monitoredAxes, captureBuffer);
        }
        else if (snapshotReader is IBulkAxisSnapshotReader bulkReader)
        {
            MotionAxisSnapshot[] snapshots = bulkReader.GetMultipleAxisSnapshots(monitoredAxes);
            snapshots.CopyTo(captureBuffer, 0);
        }
        else
        {
            for (int i = 0; i < monitoredAxes.Length; i++)
            {
                captureBuffer[i] = snapshotReader.GetAxisSnapshot(monitoredAxes[i]);
            }
        }

        for (int i = 0; i < monitoredAxes.Length; i++)
        {
            short axis = monitoredAxes[i];
            var snapshot = captureBuffer[i];
            var previousSnapshot = snapshots.TryGetValue(axis, out var previous)
                ? previous
                : (MotionAxisSnapshot?)null;

            snapshots[axis] = snapshot;
            AxisSnapshotCaptured?.Invoke(snapshot);

            if (previousSnapshot is null)
            {
                if (options.RaiseInitialSnapshotChanged)
                {
                    AxisSnapshotChanged?.Invoke(this, new MotionAxisSnapshotChangedEventArgs(snapshot, null));
                }

                continue;
            }

            if (!snapshot.HasSameState(previousSnapshot.Value))
            {
                AxisSnapshotChanged?.Invoke(this, new MotionAxisSnapshotChangedEventArgs(snapshot, previousSnapshot));
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}
