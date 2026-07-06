using Kwy.Vision.Abstractions.Images;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kwy.Vision.WPF.Images;

public static class VisionImageThumbnailFactory
{
    public static ImageSource? CreateFromImage(IVisionImage image, byte[]? pixels, int maxWidth, int maxHeight)
    {
        if (image is IVisionImageThumbnailProvider provider)
        {
            ImageSource? thumbnail = provider.CreateThumbnail(maxWidth, maxHeight);
            if (thumbnail != null)
            {
                return thumbnail;
            }
        }

        return pixels is { Length: > 0 }
            ? CreateFromPixels(pixels, image.Width, image.Height, image.Stride, image.PixelFormat, maxWidth, maxHeight)
            : null;
    }

    public static ImageSource? CreateFromPixels(
        byte[] pixels,
        int width,
        int height,
        int stride,
        VisionPixelFormat pixelFormat,
        int maxWidth,
        int maxHeight)
    {
        if (pixels.Length == 0 || width <= 0 || height <= 0 || stride <= 0)
        {
            return null;
        }

        try
        {
            BitmapSource source = BitmapSource.Create(
                width,
                height,
                96,
                96,
                ToWpfPixelFormat(pixelFormat),
                null,
                pixels,
                stride);

            var thumbnail = new TransformedBitmap(source, new ScaleTransform(
                Math.Min(1.0, maxWidth / (double)width),
                Math.Min(1.0, maxHeight / (double)height)));
            thumbnail.Freeze();
            return thumbnail;
        }
        catch
        {
            return null;
        }
    }

    private static PixelFormat ToWpfPixelFormat(VisionPixelFormat pixelFormat)
        => pixelFormat switch
        {
            VisionPixelFormat.Mono8 => PixelFormats.Gray8,
            VisionPixelFormat.Bgr24 => PixelFormats.Bgr24,
            VisionPixelFormat.Bgra32 => PixelFormats.Bgra32,
            VisionPixelFormat.Rgb24 => PixelFormats.Rgb24,
            _ => throw new NotSupportedException($"Unsupported vision pixel format: {pixelFormat}.")
        };
}
