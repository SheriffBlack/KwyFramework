using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconBlobInspectionAlgorithm
    : HalconVisionAlgorithm<BlobInspectionRequest, BlobInspectionResult>
{
    public const string Id = "BlobInspection";

    private static readonly string[] CachedBlobNames = Enumerable.Range(0, 101).Select(i => $"Blob {i} Bounds").ToArray();
    private static readonly string[] CachedBlobCenterNames = Enumerable.Range(0, 101).Select(i => $"Blob {i} Center").ToArray();

    private readonly HalconVisionImageConverter converter;

    public HalconBlobInspectionAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<BlobInspectionResult>> ExecuteAsync(
        BlobInspectionRequest request,
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
            cancellationToken.ThrowIfCancellationRequested();

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

            int count = Math.Min(areas.Length, request.MaximumCount);
            var blobs = new List<VisionBlob>(count);
            var overlays = new List<IVisionOverlayShape>(count * 3);
            for (int i = 0; i < count; i++)
            {
                double left = column1[i].D;
                double top = row1[i].D;
                double width = column2[i].D - left + 1;
                double height = row2[i].D - top + 1;
                var center = new VisionPoint(columns[i].D, rows[i].D);
                var bounds = new VisionRectangle(left, top, width, height);

                blobs.Add(new VisionBlob(areas[i].D, center, bounds));

                // Add overlays for visualization
                string blobName = (i + 1 < CachedBlobNames.Length) ? CachedBlobNames[i + 1] : $"Blob {i + 1} Bounds";
                string centerName = (i + 1 < CachedBlobCenterNames.Length) ? CachedBlobCenterNames[i + 1] : $"Blob {i + 1} Center";

                overlays.Add(new OverlayRectangle(bounds, VisionColor.Green, 1.5, blobName));
                overlays.Add(new OverlayCircle(new VisionCircle(center, 3.0), VisionColor.Red, 1.5, centerName));
                overlays.Add(new OverlayText(new VisionPoint(left, top - 5), $"#{i + 1} A:{areas[i].D:F0}", VisionColor.Yellow, 12));
            }

            stopwatch.Stop();
            return VisionExecutionResult<BlobInspectionResult>.Success(
                new BlobInspectionResult(blobs),
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
            return VisionExecutionResult<BlobInspectionResult>.Failure(
                "HALCON_OPERATOR_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static void Validate(BlobInspectionRequest request)
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
