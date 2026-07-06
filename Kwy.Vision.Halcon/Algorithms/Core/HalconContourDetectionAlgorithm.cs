using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconContourDetectionAlgorithm
    : HalconVisionAlgorithm<ContourDetectionRequest, ContourDetectionResult>
{
    public const string Id = "ContourDetection";

    private static readonly string[] CachedContourNames = Enumerable.Range(0, 101).Select(i => $"Contour {i}").ToArray();

    private readonly HalconVisionImageConverter converter;

    public HalconContourDetectionAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<ContourDetectionResult>> ExecuteAsync(
        ContourDetectionRequest request,
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
            using HXLDCont edges = workingImage.EdgesSubPix(
                ToHalconFilter(request.Filter),
                request.Alpha,
                request.LowThreshold,
                request.HighThreshold);
            using HXLDCont selected = edges.SelectContoursXld(
                "contour_length",
                request.MinimumLength,
                request.MaximumLength,
                -0.5,
                0.5);

            int objectCount = Math.Min(selected.CountObj(), request.MaximumCount);
            var contours = new List<VisionDetectedContour>(objectCount);
            var overlays = new List<IVisionOverlayShape>(objectCount);
            for (int index = 1; index <= objectCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using HXLDCont contour = selected.SelectObj(index);
                contour.GetContourXld(out HTuple rows, out HTuple columns);
                HTuple lengths = contour.LengthXld();
                var points = new VisionPoint[rows.Length];
                for (int pointIndex = 0; pointIndex < rows.Length; pointIndex++)
                {
                    points[pointIndex] = new VisionPoint(columns[pointIndex].D, rows[pointIndex].D);
                }

                if (points.Length >= 2)
                {
                    var visionContour = new VisionContour(points, isClosed: false);
                    contours.Add(new VisionDetectedContour(
                        visionContour,
                        lengths.Length > 0 ? lengths[0].D : 0));
                    
                    string contourName = (index < CachedContourNames.Length) ? CachedContourNames[index] : $"Contour {index}";
                    overlays.Add(new OverlayContour(visionContour, VisionColor.Green, 1.5, contourName));
                }
            }

            stopwatch.Stop();
            return VisionExecutionResult<ContourDetectionResult>.Success(
                new ContourDetectionResult(contours),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["CandidateCount"] = selected.CountObj().ToString(),
                    ["ReturnedCount"] = contours.Count.ToString()
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
            return VisionExecutionResult<ContourDetectionResult>.Failure(
                "HALCON_CONTOUR_DETECTION_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static string ToHalconFilter(VisionEdgeFilter filter) => filter switch
    {
        VisionEdgeFilter.Canny => "canny",
        VisionEdgeFilter.Deriche1 => "deriche1",
        VisionEdgeFilter.Deriche2 => "deriche2",
        VisionEdgeFilter.Shen => "shen",
        VisionEdgeFilter.Lanser1 => "lanser1",
        VisionEdgeFilter.Lanser2 => "lanser2",
        VisionEdgeFilter.Mshen => "mshen",
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
    };

    private static void Validate(ContourDetectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        if (!double.IsFinite(request.Alpha) || request.Alpha <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Alpha));
        }

        if (!double.IsFinite(request.LowThreshold)
            || !double.IsFinite(request.HighThreshold)
            || request.LowThreshold < 0
            || request.LowThreshold > request.HighThreshold)
        {
            throw new ArgumentException("Invalid contour threshold range.", nameof(request));
        }

        if (!double.IsFinite(request.MinimumLength)
            || !double.IsFinite(request.MaximumLength)
            || request.MinimumLength < 0
            || request.MinimumLength > request.MaximumLength)
        {
            throw new ArgumentException("Invalid contour length range.", nameof(request));
        }

        if (request.MaximumCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumCount));
        }
    }
}
