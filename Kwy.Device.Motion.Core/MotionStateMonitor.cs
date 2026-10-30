using System.Collections.Concurrent;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Core.StateSources;

namespace Kwy.Device.Motion.Core;

/// <summary>
/// 缓存运动状态源产生的轴快照，并对外发布统一的采集与变化事件。
/// 本类不关心数据来自 SDK 轮询、专用线程、厂商回调、EtherCAT PDO 还是 PLC 状态区。
/// </summary>
public sealed class MotionStateMonitor : IMotionStateMonitor
{
    private readonly IMotionStateSource stateSource;
    private readonly MotionStateMonitorOptions options;
    private readonly short[] monitoredAxes;
    private readonly HashSet<short> monitoredAxisSet;
    private readonly ConcurrentDictionary<short, MotionAxisSnapshot> snapshots = new();
    private readonly object snapshotSync = new();
    private bool disposed;

    /// <summary>使用 Core 内置的轮询或专用线程状态源。</summary>
    public MotionStateMonitor(IAxisSnapshotReader snapshotReader, MotionStateMonitorOptions options)
        : this(CreateDefaultSource(snapshotReader, options), options)
    {
    }

    /// <summary>使用厂商适配器提供的回调、PDO、PLC 或混合状态源。</summary>
    public MotionStateMonitor(IMotionStateSource stateSource, MotionStateMonitorOptions options)
    {
        this.stateSource = stateSource ?? throw new ArgumentNullException(nameof(stateSource));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateMonitorOptions(this.options);
        monitoredAxes = this.options.GetAxes().ToArray();
        monitoredAxisSet = monitoredAxes.ToHashSet();
        this.stateSource.SnapshotCaptured += OnSnapshotCaptured;
        this.stateSource.SourceErrorOccurred += OnSourceErrorOccurred;
    }

    public bool IsRunning => stateSource.IsRunning;

    public MotionStateAcquisitionMode AcquisitionMode => stateSource.AcquisitionMode;

    public event Action<MotionAxisSnapshot>? AxisSnapshotCaptured;

    public event EventHandler<MotionAxisSnapshotChangedEventArgs>? AxisSnapshotChanged;

    public event EventHandler<ErrorOccurredEventArgs>? MonitorErrorOccurred;

    public MotionAxisSnapshot GetAxisSnapshot(short axis)
    {
        ThrowIfDisposed();
        return snapshots.TryGetValue(axis, out MotionAxisSnapshot snapshot)
            ? snapshot
            : throw new KeyNotFoundException($"Axis {axis} snapshot has not been captured yet.");
    }

    public IReadOnlyDictionary<short, MotionAxisSnapshot> GetAllAxisSnapshots()
    {
        ThrowIfDisposed();
        return snapshots.ToDictionary(static pair => pair.Key, static pair => pair.Value);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await stateSource.StartAsync(cancellationToken).ConfigureAwait(false);

        short[] missingAxes = monitoredAxes.Where(axis => !snapshots.ContainsKey(axis)).ToArray();
        if (missingAxes.Length == 0)
        {
            return;
        }

        await stateSource.StopAsync(CancellationToken.None).ConfigureAwait(false);
        throw new InvalidOperationException(
            $"Motion state source '{stateSource.GetType().Name}' did not publish an initial snapshot for axes: {string.Join(", ", missingAxes)}.");
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => stateSource.StopAsync(cancellationToken);

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

        stateSource.SnapshotCaptured -= OnSnapshotCaptured;
        stateSource.SourceErrorOccurred -= OnSourceErrorOccurred;
        await stateSource.DisposeAsync().ConfigureAwait(false);
        disposed = true;
    }

    private void OnSnapshotCaptured(MotionAxisSnapshot snapshot)
    {
        if (!monitoredAxisSet.Contains(snapshot.Axis))
        {
            PublishMonitorError(
                new InvalidOperationException($"State source published unconfigured axis {snapshot.Axis}."),
                $"Motion state source published unconfigured axis {snapshot.Axis}.");
            return;
        }

        lock (snapshotSync)
        {
            MotionAxisSnapshot? previousSnapshot = snapshots.TryGetValue(snapshot.Axis, out MotionAxisSnapshot previous)
                ? previous
                : null;

            snapshots[snapshot.Axis] = snapshot;
            PublishSnapshotCaptured(snapshot);

            if (previousSnapshot is null)
            {
                if (options.RaiseInitialSnapshotChanged)
                {
                    PublishSnapshotChanged(new MotionAxisSnapshotChangedEventArgs(snapshot, null));
                }

                return;
            }

            if (!snapshot.HasSameState(previousSnapshot.Value))
            {
                PublishSnapshotChanged(new MotionAxisSnapshotChangedEventArgs(snapshot, previousSnapshot));
            }
        }
    }

    private void OnSourceErrorOccurred(object? sender, ErrorOccurredEventArgs args)
        => PublishMonitorError(args.Exception, args.Message);

    private void PublishSnapshotCaptured(MotionAxisSnapshot snapshot)
    {
        Delegate[] subscribers = AxisSnapshotCaptured?.GetInvocationList() ?? [];
        foreach (Action<MotionAxisSnapshot> subscriber in subscribers.Cast<Action<MotionAxisSnapshot>>())
        {
            try
            {
                subscriber(snapshot);
            }
            catch (Exception exception)
            {
                PublishMonitorError(exception, $"Axis {snapshot.Axis} snapshot subscriber failed: {exception.Message}");
            }
        }
    }

    private void PublishSnapshotChanged(MotionAxisSnapshotChangedEventArgs args)
    {
        Delegate[] subscribers = AxisSnapshotChanged?.GetInvocationList() ?? [];
        foreach (EventHandler<MotionAxisSnapshotChangedEventArgs> subscriber in subscribers.Cast<EventHandler<MotionAxisSnapshotChangedEventArgs>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception exception)
            {
                PublishMonitorError(exception, $"Axis {args.Snapshot.Axis} change subscriber failed: {exception.Message}");
            }
        }
    }

    private void PublishMonitorError(Exception exception, string message)
    {
        var args = new ErrorOccurredEventArgs(exception, message);
        Delegate[] subscribers = MonitorErrorOccurred?.GetInvocationList() ?? [];
        foreach (EventHandler<ErrorOccurredEventArgs> subscriber in subscribers.Cast<EventHandler<ErrorOccurredEventArgs>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch
            {
            }
        }
    }

    private static IMotionStateSource CreateDefaultSource(
        IAxisSnapshotReader snapshotReader,
        MotionStateMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshotReader);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return options.AcquisitionMode switch
        {
            MotionStateAcquisitionMode.Polling => new PollingMotionStateSource(snapshotReader, options),
            MotionStateAcquisitionMode.DedicatedThread => new DedicatedThreadMotionStateSource(snapshotReader, options),
            _ => throw new InvalidOperationException(
                $"Acquisition mode '{options.AcquisitionMode}' requires an explicit vendor IMotionStateSource.")
        };
    }

    private static void ValidateMonitorOptions(MotionStateMonitorOptions options)
    {
        if (options.PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("PollInterval must be greater than zero.");
        }

        IReadOnlyCollection<short> axes = options.GetAxes();
        if (axes.Any(static axis => axis < 1))
        {
            throw new InvalidOperationException("Monitored axes must be greater than or equal to 1.");
        }

        if (axes.Distinct().Count() != axes.Count)
        {
            throw new InvalidOperationException("Monitored axes cannot contain duplicates.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
