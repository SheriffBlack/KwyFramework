using System.Runtime.InteropServices;
using HalconDotNet;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.Halcon.Images;

/// <summary>
/// Owns one HALCON image without exposing HImage through the public vision contracts.
/// </summary>
public sealed class HalconVisionImage : IVisionImage
{
    private HImage? image;

    internal HalconVisionImage(
        HImage image,
        VisionPixelFormat pixelFormat,
        DateTimeOffset timestamp)
    {
        this.image = image ?? throw new ArgumentNullException(nameof(image));
        image.GetImageSize(out int width, out int height);
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        Stride = VisionPixelFormatInfo.GetMinimumStride(width, pixelFormat);
        Timestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
    }

    public string BackendId => VisionBackendIds.Halcon;

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public VisionPixelFormat PixelFormat { get; }

    public DateTimeOffset Timestamp { get; }

    public bool IsDisposed => image == null;

    internal HImage NativeImage
        => image ?? throw new ObjectDisposedException(nameof(HalconVisionImage));

    internal HalconVisionImage Clone()
        => new(NativeImage.CopyImage(), PixelFormat, Timestamp);

    public ValueTask<ReadOnlyMemory<byte>> GetPixelMemoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HImage native = NativeImage;

        byte[] pixels = PixelFormat switch
        {
            VisionPixelFormat.Mono8 => CopyMono8(native),
            VisionPixelFormat.Mono16 => CopyMono16(native),
            VisionPixelFormat.Rgb24 or VisionPixelFormat.Bgr24 => CopyColor24(native, PixelFormat),
            _ => throw new NotSupportedException(
                $"HALCON pixel export for {PixelFormat} is not supported.")
        };

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(pixels);
    }

    public void Dispose()
    {
        HImage? current = Interlocked.Exchange(ref image, null);
        current?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private byte[] CopyMono8(HImage native)
    {
        IntPtr pointer = native.GetImagePointer1(out string type, out int width, out int height);
        if (!string.Equals(type, "byte", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected HALCON byte image, but received '{type}'.");
        }

        var result = new byte[checked(width * height)];
        Marshal.Copy(pointer, result, 0, result.Length);
        return result;
    }

    private byte[] CopyMono16(HImage native)
    {
        IntPtr pointer = native.GetImagePointer1(out string type, out int width, out int height);
        if (!string.Equals(type, "uint2", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected HALCON uint2 image, but received '{type}'.");
        }

        var words = new short[checked(width * height)];
        Marshal.Copy(pointer, words, 0, words.Length);
        var result = new byte[checked(words.Length * sizeof(short))];
        Buffer.BlockCopy(words, 0, result, 0, result.Length);
        return result;
    }

    private static byte[] CopyColor24(HImage native, VisionPixelFormat format)
    {
        native.GetImagePointer3(
            out IntPtr red,
            out IntPtr green,
            out IntPtr blue,
            out string type,
            out int width,
            out int height);
        if (!string.Equals(type, "byte", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected HALCON byte color image, but received '{type}'.");
        }

        int planeLength = checked(width * height);
        var r = new byte[planeLength];
        var g = new byte[planeLength];
        var b = new byte[planeLength];
        Marshal.Copy(red, r, 0, planeLength);
        Marshal.Copy(green, g, 0, planeLength);
        Marshal.Copy(blue, b, 0, planeLength);

        var result = new byte[checked(planeLength * 3)];
        bool rgb = format == VisionPixelFormat.Rgb24;
        for (int i = 0; i < planeLength; i++)
        {
            int offset = i * 3;
            result[offset] = rgb ? r[i] : b[i];
            result[offset + 1] = g[i];
            result[offset + 2] = rgb ? b[i] : r[i];
        }

        return result;
    }
}
