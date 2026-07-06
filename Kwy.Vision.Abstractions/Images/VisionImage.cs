namespace Kwy.Vision.Abstractions.Images;

public enum VisionPixelFormat
{
    Unknown,
    Mono8,
    Mono16,
    Bgr24,
    Rgb24,
    Bgra32,
    Rgba32
}

public static class VisionPixelFormatInfo
{
    public static int GetBytesPerPixel(VisionPixelFormat pixelFormat) => pixelFormat switch
    {
        VisionPixelFormat.Mono8 => 1,
        VisionPixelFormat.Mono16 => 2,
        VisionPixelFormat.Bgr24 or VisionPixelFormat.Rgb24 => 3,
        VisionPixelFormat.Bgra32 or VisionPixelFormat.Rgba32 => 4,
        _ => throw new ArgumentOutOfRangeException(
            nameof(pixelFormat),
            pixelFormat,
            "The pixel format does not have a known packed bytes-per-pixel value.")
    };

    public static int GetMinimumStride(int width, VisionPixelFormat pixelFormat)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        return checked(width * GetBytesPerPixel(pixelFormat));
    }
}

/// <summary>
/// Represents an owned vision image. Implementations may keep pixels in managed memory
/// or wrap a backend-native image without exposing its native type.
/// </summary>
public interface IVisionImage : IDisposable, IAsyncDisposable
{
    string BackendId { get; }

    int Width { get; }

    int Height { get; }

    int Stride { get; }

    VisionPixelFormat PixelFormat { get; }

    DateTimeOffset Timestamp { get; }

    bool IsDisposed { get; }

    ValueTask<ReadOnlyMemory<byte>> GetPixelMemoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>A backend-independent image backed by owned managed memory.</summary>
public sealed class VisionImageBuffer : IVisionImage
{
    public const string ManagedBackendId = "Managed";

    private ReadOnlyMemory<byte> pixels;
    private bool disposed;

    public VisionImageBuffer(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        VisionPixelFormat pixelFormat,
        DateTimeOffset timestamp = default)
    {
        int resolvedStride = stride > 0
            ? stride
            : VisionPixelFormatInfo.GetMinimumStride(width, pixelFormat);
        ValidateDimensions(pixels.Length, width, height, resolvedStride);
        this.pixels = pixels.ToArray();
        Width = width;
        Height = height;
        Stride = resolvedStride;
        PixelFormat = pixelFormat;
        Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
    }

    public string BackendId => ManagedBackendId;

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
        return ValueTask.FromResult(pixels);
    }

    public void Dispose()
    {
        disposed = true;
        pixels = ReadOnlyMemory<byte>.Empty;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static void ValidateDimensions(int pixelLength, int width, int height, int stride)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        if ((long)stride * height > pixelLength)
        {
            throw new ArgumentException("Pixel memory is smaller than stride multiplied by height.", nameof(pixelLength));
        }
    }
}

public interface IVisionImageConverter
{
    string BackendId { get; }

    ValueTask<IVisionImage> ConvertAsync(
        IVisionImage source,
        CancellationToken cancellationToken = default);
}

public interface IVisionImageConverterRegistry
{
    IReadOnlyCollection<IVisionImageConverter> Converters { get; }

    IVisionImageConverter GetRequired(string backendId);
}

public sealed class VisionImageConverterRegistry : IVisionImageConverterRegistry
{
    private readonly IReadOnlyDictionary<string, IVisionImageConverter> converters;

    public VisionImageConverterRegistry(IEnumerable<IVisionImageConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        var byBackend = new Dictionary<string, IVisionImageConverter>(StringComparer.OrdinalIgnoreCase);
        foreach (IVisionImageConverter converter in converters)
        {
            if (!byBackend.TryAdd(converter.BackendId, converter))
            {
                throw new InvalidOperationException(
                    $"Vision image converter for backend '{converter.BackendId}' is already registered.");
            }
        }

        this.converters = byBackend;
        Converters = byBackend.Values.ToArray();
    }

    public IReadOnlyCollection<IVisionImageConverter> Converters { get; }

    public IVisionImageConverter GetRequired(string backendId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        return converters.TryGetValue(backendId, out IVisionImageConverter? converter)
            ? converter
            : throw new KeyNotFoundException($"Vision image converter for backend '{backendId}' is not registered.");
    }
}
