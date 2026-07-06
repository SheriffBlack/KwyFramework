using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Internal;
using Kwy.Vision.Halcon.Models;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconShapeMatchingAlgorithm
    : HalconVisionAlgorithm<ShapeMatchingRequest, ShapeMatchingResult>
{
    public const string Id = "ShapeMatching";

    private static readonly string[] CachedMatchContours = Enumerable.Range(0, 101).Select(i => $"Match {i} Contour").ToArray();
    private static readonly string[] CachedMatchCenters = Enumerable.Range(0, 101).Select(i => $"Match {i} Center").ToArray();

    private readonly HalconVisionImageConverter converter;
    private readonly HalconShapeModelRepository models;

    public HalconShapeMatchingAlgorithm(
        HalconVisionImageConverter converter,
        HalconShapeModelRepository models)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
        this.models = models ?? throw new ArgumentNullException(nameof(models));
    }

    public override async ValueTask<VisionExecutionResult<ShapeMatchingResult>> ExecuteAsync(
        ShapeMatchingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using HalconImageLease imageLease = await converter
                .AcquireAsync(request.Image, cancellationToken)
                .ConfigureAwait(false);
            using HalconShapeModelRepository.ModelLease modelLease = await models
                .AcquireAsync(request.TemplateId, cancellationToken)
                .ConfigureAwait(false);
            using HRegion? searchRegion = HalconRegionFactory.Create(request.SearchRegion);
            using HImage searchImage = searchRegion == null
                ? imageLease.Image.CopyImage()
                : imageLease.Image.ReduceDomain(searchRegion);

            searchImage.FindShapeModel(
                modelLease.Model,
                request.AngleStartRadians,
                request.AngleExtentRadians,
                request.MinimumScore,
                request.MaximumMatches,
                request.MaximumOverlap,
                "least_squares",
                0,
                0.9,
                out HTuple rows,
                out HTuple columns,
                out HTuple angles,
                out HTuple scores);

            using HXLDCont modelContours = modelLease.Model.GetShapeModelContours(1);
            int countObj = modelContours.CountObj();

            var matches = new List<VisionShapeMatch>(scores.Length);
            // Pre-allocate overlay list capacity to prevent resizing overhead
            var overlays = new List<IVisionOverlayShape>(scores.Length * (countObj + 2));
            for (int i = 0; i < scores.Length; i++)
            {
                double row = rows[i].D;
                double col = columns[i].D;
                double angle = angles[i].D;

                matches.Add(new VisionShapeMatch(
                    new VisionPose2D(col, row, angle),
                    scores[i].D));

                // Transform model contour to match position
                var homMat = new HHomMat2D();
                homMat.HomMat2dIdentity();
                homMat = homMat.HomMat2dTranslate(row, col);
                homMat = homMat.HomMat2dRotate(angle, row, col);

                using HXLDCont transformedContours = modelContours.AffineTransContourXld(homMat);
                int numContours = transformedContours.CountObj();
                string contourName = (i + 1 < CachedMatchContours.Length) ? CachedMatchContours[i + 1] : $"Match {i + 1} Contour";
                for (int c = 1; c <= numContours; c++)
                {
                    using HXLDCont contour = transformedContours.SelectObj(c);
                    contour.GetContourXld(out HTuple rPoints, out HTuple cPoints);
                    var points = new VisionPoint[rPoints.Length];
                    for (int p = 0; p < rPoints.Length; p++)
                    {
                        points[p] = new VisionPoint(cPoints[p].D, rPoints[p].D);
                    }
                    if (points.Length >= 2)
                    {
                        overlays.Add(new OverlayContour(new VisionContour(points, isClosed: false), VisionColor.Green, 1.5, contourName));
                    }
                }

                // Add center point and label text overlays
                var center = new VisionPoint(col, row);
                string centerName = (i + 1 < CachedMatchCenters.Length) ? CachedMatchCenters[i + 1] : $"Match {i + 1} Center";
                overlays.Add(new OverlayCircle(new VisionCircle(center, 4.0), VisionColor.Red, 1.5, centerName));
                overlays.Add(new OverlayText(new VisionPoint(col + 10, row - 10), $"#{i + 1} ({scores[i].D:F2})", VisionColor.Yellow, 12));
            }

            stopwatch.Stop();
            return VisionExecutionResult<ShapeMatchingResult>.Success(
                new ShapeMatchingResult(matches),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["TemplateId"] = request.TemplateId,
                    ["MatchCount"] = matches.Count.ToString()
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
            return VisionExecutionResult<ShapeMatchingResult>.Failure(
                "HALCON_OPERATOR_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static void Validate(ShapeMatchingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TemplateId);
        if (!double.IsFinite(request.AngleStartRadians)
            || !double.IsFinite(request.AngleExtentRadians))
        {
            throw new ArgumentException("Shape matching angle range must be finite.", nameof(request));
        }

        if (!double.IsFinite(request.MinimumScore) || request.MinimumScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MinimumScore));
        }

        if (request.MaximumMatches < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumMatches));
        }

        if (!double.IsFinite(request.MaximumOverlap) || request.MaximumOverlap is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumOverlap));
        }
    }
}
