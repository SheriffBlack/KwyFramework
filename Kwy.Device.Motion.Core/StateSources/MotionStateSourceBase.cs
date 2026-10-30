using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;

namespace Kwy.Device.Motion.Core.StateSources;

/// <summary>
/// 运动状态源的生命周期与安全事件发布基类。
/// 厂商回调、EtherCAT PDO 和混合采集适配器可继承本类，
/// 只实现自己的启停与数据映射。
/// </summary>
public abstract class MotionStateSourceBase : IMotionStateSource
{
    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);
    private int running;
    private bool disposed;

    public abstract MotionStateAcquisitionMode AcquisitionMode { get; }

    public bool IsRunning => Volatile.Read(ref running) != 0;

    public event Action<MotionAxisSnapshot>? SnapshotCaptured;

    public event EventHandler<ErrorOccurredEventArgs>? SourceErrorOccurred;

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

            Volatile.Write(ref running, 1);
            try
            {
                await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                }

                Volatile.Write(ref running, 0);
                throw;
            }
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
            if (!IsRunning)
            {
                return;
            }

            try
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref running, 0);
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
        GC.SuppressFinalize(this);
    }

    protected abstract Task StartCoreAsync(CancellationToken cancellationToken);

    protected abstract Task StopCoreAsync(CancellationToken cancellationToken);

    /// <summary>发布已转换为框架统一模型的轴快照。</summary>
    protected void PublishSnapshot(MotionAxisSnapshot snapshot)
    {
        Delegate[] subscribers = SnapshotCaptured?.GetInvocationList() ?? [];
        foreach (Action<MotionAxisSnapshot> subscriber in subscribers.Cast<Action<MotionAxisSnapshot>>())
        {
            try
            {
                subscriber(snapshot);
            }
            catch (Exception exception)
            {
                PublishError(exception, $"Motion state source subscriber failed for axis {snapshot.Axis}: {exception.Message}");
            }
        }
    }

    /// <summary>发布采集、通信或快照转换错误。</summary>
    protected void PublishError(Exception exception, string message)
    {
        var args = new ErrorOccurredEventArgs(exception, message);
        Delegate[] subscribers = SourceErrorOccurred?.GetInvocationList() ?? [];
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
