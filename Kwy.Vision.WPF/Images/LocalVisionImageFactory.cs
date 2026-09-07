using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.WPF.Sources;

namespace Kwy.Vision.WPF.Images;

public sealed class LocalVisionImageFactory : ILocalVisionImageFactory
{
    public IReadOnlyList<IVisionImage> CreateImages(string? source)
    {
        var frameSource = new LocalImageFrameSource(source);
        int count = frameSource.FrameCount ?? 0;
        if (count == 0)
        {
            return Array.Empty<IVisionImage>();
        }

        var images = new List<IVisionImage>(count);
        for (int i = 0; i < count; i++)
        {
            VisionFrame? frame = frameSource.ReadFrameAsync(i).AsTask().GetAwaiter().GetResult();
            if (frame != null)
            {
                images.Add(frame.Image);
            }
        }

        return images;
    }
}
