using Kwy.Vision.Abstractions.Images;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kwy.Vision.WPF.Images;

public sealed class LocalFileVisionImage : IVisionImage, IVisionImageThumbnailProvider
{
    private readonly object gate = new();
    private byte[]? pixels;
    private bool disposed;

    public LocalFileVisionImage(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;

        using var stream = File.OpenRead(filePath);
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        BitmapSource source = decoder.Frames[0];

        Width = source.PixelWidth;
        Height = source.PixelHeight;
        PixelFormat = source.Format == PixelFormats.Gray8
            ? VisionPixelFormat.Mono8
            : VisionPixelFormat.Bgra32;
        Stride = Width * VisionPixelFormatInfo.GetBytesPerPixel(PixelFormat);
        Timestamp = File.GetLastWriteTimeUtc(filePath);
    }

    public string FilePath { get; }

    public string BackendId => "LocalFile";

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

        if (pixels != null)
        {
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(pixels);
        }

        lock (gate)
        {
            if (pixels == null)
            {
                pixels = DecodePixels(FilePath);
            }
        }

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(pixels);
    }

    public ImageSource? CreateThumbnail(int maxWidth, int maxHeight)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            int decodeWidth = Width >= Height
                ? maxWidth
                : Math.Max(1, (int)Math.Round(maxHeight * (double)Width / Math.Max(1, Height)));

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = Math.Max(1, decodeWidth);
            image.UriSource = new Uri(FilePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        disposed = true;
        pixels = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private byte[] DecodePixels(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        BitmapSource converted = PixelFormat == VisionPixelFormat.Mono8
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var buffer = new byte[Stride * Height];
        converted.CopyPixels(buffer, Stride, 0);
        return buffer;
    }
}
