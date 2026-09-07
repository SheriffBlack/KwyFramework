using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.WPF.Images;

public interface ILocalVisionImageFactory
{
    IReadOnlyList<IVisionImage> CreateImages(string? source);
}
