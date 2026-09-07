using HalconDotNet;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Halcon.Images;

/// <summary>
/// Provides implementation-layer access to an HImage while preserving ownership rules.
/// </summary>
public sealed class HalconImageLease : IAsyncDisposable
{
    private readonly HalconVisionImage? ownedImage;

    private HalconImageLease(HalconVisionImage image, bool ownsImage)
    {
        VisionImage = image;
        ownedImage = ownsImage ? image : null;
    }

    internal HalconVisionImage VisionImage { get; }

    public HImage Image => VisionImage.NativeImage;

    internal static async ValueTask<HalconImageLease> CreateAsync(
        IVisionImage source,
        HalconVisionImageConverter converter,
        CancellationToken cancellationToken)
    {
        if (source is HalconVisionImage halconImage)
        {
            return new HalconImageLease(halconImage, ownsImage: false);
        }

        var converted = (HalconVisionImage)await converter
            .ConvertAsync(source, cancellationToken)
            .ConfigureAwait(false);
        return new HalconImageLease(converted, ownsImage: true);
    }

    public ValueTask DisposeAsync()
        => ownedImage?.DisposeAsync() ?? ValueTask.CompletedTask;
}
