using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconBlobFeatureInspectionAlgorithm
    : HalconVisionAlgorithm<BlobFeatureInspectionRequest, BlobFeatureInspectionResult>
{
    public const string Id = "BlobFeatureInspection";

    private readonly HalconVisionImageConverter converter;

    public HalconBlobFeatureInspectionAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<BlobFeatureInspectionResult>> ExecuteAsync(
        BlobFeatureInspectionRequest request,
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
            using HRegion? searchRegion = HalconRegionFactory.Create(request.SearchRegion);
            using HImage workingImage = searchRegion == null
                ? lease.Image.CopyImage()
                : lease.Image.ReduceDomain(searchRegion);
            using HRegion threshold = workingImage.Threshold(request.MinimumGray, request.MaximumGray);
            using HRegion connected = threshold.Connection();
            using HRegion selected = connected.SelectShape(
                "area",
                "and",
                request.MinimumArea,
                request.MaximumArea);

            HTuple areas = selected.AreaCenter(out HTuple rows, out HTuple columns);
            selected.SmallestRectangle1(out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
            selected.SmallestRectangle2(out HTuple rectRows, out HTuple rectColumns, out HTuple phis, out HTuple length1, out HTuple length2);
            HTuple circularities = selected.Circularity();
            HTuple contourLengths = selected.Contlength();
            selected.Roundness(out HTuple _, out HTuple roundnessValues, out HTuple _);
            HTuple meanGray = selected.Intensity(workingImage, out HTuple deviation);
            selected.MinMaxGray(workingImage, 0, out HTuple minimumGray, out HTuple maximumGray, out HTuple rangeGray);
            _ = deviation;

            int count = Math.Min(areas.Length, request.MaximumCount);
            var blobs = new List<VisionBlobFeature>(count);
            var overlays = new List<IVisionOverlayShape>(count * 2);
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var center = new VisionPoint(columns[i].D, rows[i].D);
                var bounds = new VisionRectangle(
                    column1[i].D,
                    row1[i].D,
                    column2[i].D - column1[i].D + 1,
                    row2[i].D - row1[i].D + 1);
                var orientedBounds = new VisionRotatedRectangle(
                    new VisionPoint(rectColumns[i].D, rectRows[i].D),
                    length1[i].D * 2,
                    length2[i].D * 2,
                    phis[i].D);

                blobs.Add(new VisionBlobFeature(
                    areas[i].D,
                    center,
                    bounds,
                    orientedBounds,
                    circularities[i].D,
                    roundnessValues[i].D,
                    contourLengths[i].D,
                    meanGray[i].D,
                    minimumGray[i].D,
                    maximumGray[i].D,
                    rangeGray[i].D));

                overlays.Add(new OverlayRectangle(bounds, VisionColor.Green, 1.5, $"Blob {i + 1} Bounds"));
                overlays.Add(new OverlayCircle(new VisionCircle(center, 3), VisionColor.Red, 1.5, $"Blob {i + 1} Center"));
            }

            stopwatch.Stop();
            return VisionExecutionResult<BlobFeatureInspectionResult>.Success(
                new BlobFeatureInspectionResult(blobs),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["CandidateCount"] = areas.Length.ToString(),
                    ["ReturnedCount"] = blobs.Count.ToString()
                },
                overlays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return VisionExecutionResult<BlobFeatureInspectionResult>.Failure(
                "HALCON_BLOB_FEATURE_INSPECTION_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static void Validate(BlobFeatureInspectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        if (!double.IsFinite(request.MinimumGray)
            || !double.IsFinite(request.MaximumGray)
            || request.MinimumGray > request.MaximumGray)
        {
            throw new ArgumentException("Invalid gray range.", nameof(request));
        }

        if (!double.IsFinite(request.MinimumArea)
            || !double.IsFinite(request.MaximumArea)
            || request.MinimumArea < 0
            || request.MinimumArea > request.MaximumArea)
        {
            throw new ArgumentException("Invalid area range.", nameof(request));
        }

        if (request.MaximumCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumCount));
        }
    }
}
