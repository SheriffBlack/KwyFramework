using Kwy.Vision.Abstractions.Images;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kwy.Vision.WPF.Rendering;

/// <summary>
/// Reuses a <see cref="WriteableBitmap"/> and copies packed vision pixels into its back buffer.
/// </summary>
public sealed class WriteableBitmapRenderer
{
    private WriteableBitmap? bitmap;

    public WriteableBitmap? Bitmap => bitmap;

    public WriteableBitmap Render(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        VisionPixelFormat pixelFormat)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        int minimumStride = VisionPixelFormatInfo.GetMinimumStride(width, pixelFormat);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), $"Stride must be at least {minimumStride}.");
        }

        int requiredLength = checked(stride * height);
        if (pixels.Length < requiredLength)
        {
            throw new ArgumentException($"Pixel memory must contain at least {requiredLength} bytes.", nameof(pixels));
        }

        PixelFormat format = ToWpfPixelFormat(pixelFormat);
        if (bitmap == null ||
            bitmap.PixelWidth != width ||
            bitmap.PixelHeight != height ||
            bitmap.Format != format)
        {
            bitmap = new WriteableBitmap(width, height, 96, 96, format, null);
        }

        byte[]? rented = null;
        ArraySegment<byte> source;
        if (!MemoryMarshal.TryGetArray(pixels, out source) || source.Array == null)
        {
            rented = ArrayPool<byte>.Shared.Rent(requiredLength);
            pixels.Span[..requiredLength].CopyTo(rented);
            source = new ArraySegment<byte>(rented, 0, requiredLength);
        }

        bitmap.Lock();
        try
        {
            for (int row = 0; row < height; row++)
            {
                IntPtr destination = IntPtr.Add(bitmap.BackBuffer, checked(row * bitmap.BackBufferStride));
                Marshal.Copy(source.Array!, source.Offset + checked(row * stride), destination, minimumStride);
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        return bitmap;
    }

    public static PixelFormat ToWpfPixelFormat(VisionPixelFormat pixelFormat)
        => pixelFormat switch
        {
            VisionPixelFormat.Mono8 => PixelFormats.Gray8,
            VisionPixelFormat.Mono16 => PixelFormats.Gray16,
            VisionPixelFormat.Bgr24 => PixelFormats.Bgr24,
            VisionPixelFormat.Rgb24 => PixelFormats.Rgb24,
            VisionPixelFormat.Bgra32 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException($"Unsupported WPF vision pixel format: {pixelFormat}.")
        };
}
