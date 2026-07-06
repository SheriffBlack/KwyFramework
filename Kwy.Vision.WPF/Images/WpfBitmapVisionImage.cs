using Kwy.Vision.Abstractions.Images;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kwy.Vision.WPF.Images;

public sealed class WpfBitmapVisionImage : IVisionImage, IVisionImageThumbnailProvider
{
    private byte[] pixels;
    private bool disposed;

    public WpfBitmapVisionImage(
        BitmapSource source,
        string backendId = "WPF",
        DateTimeOffset timestamp = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        BitmapSource converted = source.Format == PixelFormats.Gray8
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();

        Width = converted.PixelWidth;
        Height = converted.PixelHeight;
        PixelFormat = converted.Format == PixelFormats.Gray8
            ? VisionPixelFormat.Mono8
            : VisionPixelFormat.Bgra32;
        Stride = Width * VisionPixelFormatInfo.GetBytesPerPixel(PixelFormat);

        var buffer = new byte[Stride * Height];
        converted.CopyPixels(buffer, Stride, 0);
        pixels = buffer;
        BackendId = backendId;
        Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
    }

    public string BackendId { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public VisionPixelFormat PixelFormat { get; }

    public DateTimeOffset Timestamp { get; }

    public bool IsDisposed => disposed;

    public ValueTask<ReadOnlyMemory<byte>> GetPixelMemoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(pixels);
    }

    public ImageSource? CreateThumbnail(int maxWidth, int maxHeight)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return VisionImageThumbnailFactory.CreateFromPixels(
            pixels,
            Width,
            Height,
            Stride,
            PixelFormat,
            maxWidth,
            maxHeight);
    }

    public void Dispose()
    {
        disposed = true;
        pixels = Array.Empty<byte>();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
