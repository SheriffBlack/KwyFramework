using Kwy.Vision.Abstractions.Images;

namespace KwyTemplate.Vision.Rendering;

public readonly record struct VisionLiveFrame(
    ReadOnlyMemory<byte> Pixels,
    int Width,
    int Height,
    int Stride,
    VisionPixelFormat PixelFormat,
    DateTimeOffset Timestamp = default)
{
    public void Validate()
    {
        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), Width, "Width must be greater than zero.");
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), Height, "Height must be greater than zero.");
        }

        if (Stride <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Stride), Stride, "Stride must be greater than zero.");
        }

        if ((long)Stride * Height > Pixels.Length)
        {
            throw new ArgumentException("Pixel memory is smaller than stride multiplied by height.", nameof(Pixels));
        }
    }
}
