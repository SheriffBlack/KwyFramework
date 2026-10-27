using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.WPF.Rendering;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KwyTemplate.Vision.Rendering;

public sealed class VisionLiveImageRenderer : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly WriteableBitmapRenderer renderer = new();
    private int isRendering;
    private bool disposed;

    public VisionLiveImageRenderer()
        : this(Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
    {
    }

    public VisionLiveImageRenderer(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public ImageSource? ImageSource => renderer.Bitmap;

    public bool IsInitialized => renderer.Bitmap != null;

    public long RenderedFrameCount { get; private set; }

    public long DroppedFrameCount { get; private set; }

    public event EventHandler? ImageSourceChanged;

    public bool TryRender(VisionLiveFrame frame)
    {
        ThrowIfDisposed();
        frame.Validate();

        if (Interlocked.Exchange(ref isRendering, 1) == 1)
        {
            DroppedFrameCount++;
            return false;
        }

        if (dispatcher.CheckAccess())
        {
            try
            {
                RenderCore(frame);
                return true;
            }
            finally
            {
                Volatile.Write(ref isRendering, 0);
            }
        }

        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            () =>
            {
                try
                {
                    RenderCore(frame);
                }
                finally
                {
                    Volatile.Write(ref isRendering, 0);
                }
            });

        return true;
    }

    public void Dispose()
    {
        disposed = true;
    }

    private void RenderCore(VisionLiveFrame frame)
    {
        ImageSource? previous = renderer.Bitmap;
        renderer.Render(frame.Pixels, frame.Width, frame.Height, frame.Stride, frame.PixelFormat);
        if (!ReferenceEquals(previous, renderer.Bitmap))
        {
            ImageSourceChanged?.Invoke(this, EventArgs.Empty);
        }

        RenderedFrameCount++;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
