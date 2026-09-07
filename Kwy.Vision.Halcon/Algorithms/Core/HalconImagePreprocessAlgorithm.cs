using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconImagePreprocessAlgorithm
    : HalconVisionAlgorithm<ImagePreprocessRequest, ImagePreprocessResult>
{
    public const string Id = "ImagePreprocess";

    private readonly HalconVisionImageConverter converter;

    public HalconImagePreprocessAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<ImagePreprocessResult>> ExecuteAsync(
        ImagePreprocessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using HalconImageLease lease = await converter
                .AcquireAsync(request.Image, cancellationToken)
                .ConfigureAwait(false);
            using HRegion? region = HalconRegionFactory.Create(request.Region);
            using HImage workingImage = region == null
                ? lease.Image.CopyImage()
                : lease.Image.ReduceDomain(region);

            HImage processed = Apply(workingImage, request);
            var image = new HalconVisionImage(processed, request.Image.PixelFormat, request.Image.Timestamp);

            stopwatch.Stop();
            return VisionExecutionResult<ImagePreprocessResult>.Success(
                new ImagePreprocessResult(image),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["Operation"] = request.Operation.ToString()
                });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return VisionExecutionResult<ImagePreprocessResult>.Failure(
                "HALCON_IMAGE_PREPROCESS_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static HImage Apply(HImage image, ImagePreprocessRequest request)
        => request.Operation switch
        {
            VisionPreprocessOperation.Mean => image.MeanImage(request.MaskWidth, request.MaskHeight),
            VisionPreprocessOperation.Median => image.MedianImage("circle", request.MaskWidth, "mirrored"),
            VisionPreprocessOperation.Gaussian => image.GaussImage(NormalizeOddMask(request.MaskWidth)),
            VisionPreprocessOperation.Emphasize => image.Emphasize(request.MaskWidth, request.MaskHeight, request.Factor),
            VisionPreprocessOperation.GrayOpening => image.GrayOpeningRect(request.MaskWidth, request.MaskHeight),
            VisionPreprocessOperation.GrayClosing => image.GrayClosingRect(request.MaskWidth, request.MaskHeight),
            VisionPreprocessOperation.GrayDilation => image.GrayDilationRect(request.MaskWidth, request.MaskHeight),
            VisionPreprocessOperation.GrayErosion => image.GrayErosionRect(request.MaskWidth, request.MaskHeight),
            VisionPreprocessOperation.ScaleGray => image.ScaleImage(request.Factor, request.Offset),
            VisionPreprocessOperation.GrayOpeningCircle => image.GrayOpeningShape(request.Radius, request.Radius, "circle"),
            VisionPreprocessOperation.GrayClosingCircle => image.GrayClosingShape(request.Radius, request.Radius, "circle"),
            VisionPreprocessOperation.GrayDilationCircle => image.GrayDilationShape(request.Radius, request.Radius, "circle"),
            VisionPreprocessOperation.GrayErosionCircle => image.GrayErosionShape(request.Radius, request.Radius, "circle"),
            VisionPreprocessOperation.AnisotropicDiffusion => image.AnisotropicDiffusion(
                request.Mode,
                request.Theta,
                request.Factor,
                request.Iterations),
            VisionPreprocessOperation.EqualizeHistogram => image.EquHistoImage(),
            VisionPreprocessOperation.Illuminate => image.Illuminate(request.MaskWidth, request.MaskHeight, request.Factor),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Operation), request.Operation, null)
        };

    private static int NormalizeOddMask(int value)
        => value % 2 == 0 ? value + 1 : value;

    private static void Validate(ImagePreprocessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        if (request.MaskWidth < 1 || request.MaskHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Mask dimensions must be greater than zero.");
        }

        if (!double.IsFinite(request.Factor))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Factor));
        }

        if (!double.IsFinite(request.Offset))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Offset));
        }

        if (!double.IsFinite(request.Radius) || request.Radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Radius));
        }

        if (!double.IsFinite(request.Theta) || request.Theta <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Theta));
        }

        if (request.Iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Iterations));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Mode);
    }
}
