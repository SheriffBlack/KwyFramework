using System.Runtime.InteropServices;
using HalconDotNet;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.Halcon.Images;

public sealed class HalconVisionImageConverter : IVisionImageConverter
{
    public string BackendId => VisionBackendIds.Halcon;

    public async ValueTask<IVisionImage> ConvertAsync(
        IVisionImage source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (source is HalconVisionImage halconImage)
        {
            return halconImage.Clone();
        }

        ReadOnlyMemory<byte> pixels = await source.GetPixelMemoryAsync(cancellationToken).ConfigureAwait(false);
        HImage image = CreateImage(pixels, source.Width, source.Height, source.Stride, source.PixelFormat);
        return new HalconVisionImage(image, source.PixelFormat, source.Timestamp);
    }

    /// <summary>
    /// Borrows an existing HALCON image or creates a temporary converted image.
    /// Dispose the returned lease after the HALCON operation completes.
    /// </summary>
    public ValueTask<HalconImageLease> AcquireAsync(
        IVisionImage source,
        CancellationToken cancellationToken = default)
        => HalconImageLease.CreateAsync(source, this, cancellationToken);

    internal static HImage CreateImage(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        VisionPixelFormat pixelFormat)
    {
        int minimumStride = VisionPixelFormatInfo.GetMinimumStride(width, pixelFormat);
        if (stride != minimumStride)
        {
            throw new NotSupportedException(
                $"HALCON conversion currently requires packed rows. Stride={stride}, expected={minimumStride}.");
        }

        byte[] data = pixels.ToArray();
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var image = new HImage();
            switch (pixelFormat)
            {
                case VisionPixelFormat.Mono8:
                    image.GenImage1("byte", width, height, handle.AddrOfPinnedObject());
                    break;

                case VisionPixelFormat.Mono16:
                    image.GenImage1("uint2", width, height, handle.AddrOfPinnedObject());
                    break;

                case VisionPixelFormat.Rgb24:
                    image.GenImageInterleaved(
                        handle.AddrOfPinnedObject(), "rgb", width, height, -1, "byte", width, height, 0, 0, -1, 0);
                    break;

                case VisionPixelFormat.Bgr24:
                    image.GenImageInterleaved(
                        handle.AddrOfPinnedObject(), "bgr", width, height, -1, "byte", width, height, 0, 0, -1, 0);
                    break;

                default:
                    image.Dispose();
                    throw new NotSupportedException($"HALCON conversion for {pixelFormat} is not supported.");
            }

            return image;
        }
        finally
        {
            handle.Free();
        }
    }
}
