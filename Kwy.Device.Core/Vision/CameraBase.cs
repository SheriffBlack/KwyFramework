using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Vision;

namespace Kwy.Device.Core.Vision;

/// <summary>Provides a single-flight acquisition lifecycle and managed-frame publication.</summary>
public abstract class CameraBase : DeviceBase, ICameraDevice, IFrameSource
{
    private readonly SemaphoreSlim grabbingSemaphore = new(1, 1);
    private volatile bool isGrabbing;

    protected CameraBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
    }

    public event EventHandler<CameraFrame>? FrameArrived;

    public bool IsGrabbing => isGrabbing;

    public async Task StartGrabbingAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await grabbingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isGrabbing)
            {
                return;
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Camera is not connected.");
            }

            await StartGrabbingCoreAsync(cancellationToken).ConfigureAwait(false);
            isGrabbing = true;
        }
        finally
        {
            grabbingSemaphore.Release();
        }
    }

    public async Task StopGrabbingAsync(CancellationToken cancellationToken = default)
    {
        await grabbingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!isGrabbing)
            {
                return;
            }

            await StopGrabbingCoreAsync(cancellationToken).ConfigureAwait(false);
            isGrabbing = false;
        }
        finally
        {
            grabbingSemaphore.Release();
        }
    }

    public async Task<CameraFrame> WaitForNextFrameAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var completion = new TaskCompletionSource<CameraFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CameraFrame>? handler = null;
        handler = (_, frame) =>
        {
            if (!completion.Task.IsCompleted)
            {
                CameraFrame retained = frame.Retain();
                if (!completion.TrySetResult(retained))
                {
                    retained.Dispose();
                }
            }
        };
        FrameArrived += handler;

        try
        {
            return await completion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            FrameArrived -= handler;
        }
    }

    protected abstract Task StartGrabbingCoreAsync(CancellationToken cancellationToken);

    protected abstract Task StopGrabbingCoreAsync(CancellationToken cancellationToken);

    protected bool HasFrameSubscribers => FrameArrived is not null;

    protected void RaiseFrameArrived(CameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        EventHandler<CameraFrame>? handlers = FrameArrived;
        if (handlers == null)
        {
            frame.Dispose();
            return;
        }

        foreach (EventHandler<CameraFrame> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, frame);
            }
            catch (Exception ex)
            {
                RaiseErrorOccurred("A camera frame subscriber failed.", ex);
            }
        }

        frame.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await base.DisposeAsync().ConfigureAwait(false);
        FrameArrived = null;
        grabbingSemaphore.Dispose();
    }
}
