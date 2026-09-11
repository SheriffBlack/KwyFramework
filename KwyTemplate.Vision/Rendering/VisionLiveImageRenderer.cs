using Kwy.Vision.Abstractions.Images;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KwyTemplate.Vision.Rendering;

public sealed class VisionLiveImageRenderer : IDisposable
{
    private readonly Dispatcher dispatcher;
    private WriteableBitmap? bitmap;
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

    public ImageSource? ImageSource => bitmap;

    public bool IsInitialized => bitmap != null;

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
        bitmap = null;
    }

    private void RenderCore(VisionLiveFrame frame)
    {
        PixelFormat format = ToWpfPixelFormat(frame.PixelFormat);
        if (bitmap == null ||
            bitmap.PixelWidth != frame.Width ||
            bitmap.PixelHeight != frame.Height ||
            bitmap.Format != format)
        {
            bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, format, null);
            ImageSourceChanged?.Invoke(this, EventArgs.Empty);
        }

        bitmap.Lock();
        try
        {
            int bytesToCopy = checked(frame.Stride * frame.Height);
            CopyPixels(frame.Pixels, bitmap.BackBuffer, bytesToCopy);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
            RenderedFrameCount++;
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    private static void CopyPixels(ReadOnlyMemory<byte> source, IntPtr destination, int length)
    {
        if (MemoryMarshal.TryGetArray(source, out ArraySegment<byte> segment) &&
            segment.Array != null)
        {
            Marshal.Copy(segment.Array, segment.Offset, destination, length);
            return;
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            source.Span[..length].CopyTo(rented);
            Marshal.Copy(rented, 0, destination, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static PixelFormat ToWpfPixelFormat(VisionPixelFormat pixelFormat)
        => pixelFormat switch
        {
            VisionPixelFormat.Mono8 => PixelFormats.Gray8,
            VisionPixelFormat.Mono16 => PixelFormats.Gray16,
            VisionPixelFormat.Bgr24 => PixelFormats.Bgr24,
            VisionPixelFormat.Bgra32 => PixelFormats.Bgra32,
            VisionPixelFormat.Rgb24 => PixelFormats.Rgb24,
            _ => throw new NotSupportedException($"Unsupported live pixel format: {pixelFormat}.")
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
