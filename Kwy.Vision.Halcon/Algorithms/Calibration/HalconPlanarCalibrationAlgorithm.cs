using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

/// <summary>Calculates a 2D affine mapping from image pixels to a planar world coordinate system.</summary>
public sealed class HalconPlanarCalibrationAlgorithm
    : HalconVisionAlgorithm<PlanarCalibrationRequest, PlanarCalibrationResult>
{
    public const string Id = "PlanarCalibration";

    private static readonly string[] CachedPointLabels = Enumerable.Range(0, 101).Select(i => $"Pt {i}").ToArray();
    private static readonly string[] CachedPointPrefixes = Enumerable.Range(0, 101).Select(i => $"Pt {i}: ").ToArray();

    public HalconPlanarCalibrationAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<PlanarCalibrationResult>> ExecuteAsync(
        PlanarCalibrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            VisionPointCorrespondence[] points = request.Correspondences.ToArray();
            var imageToWorld = new HHomMat2D();
            imageToWorld.VectorToHomMat2d(
                new HTuple(points.Select(item => item.ImagePoint.X).ToArray()),
                new HTuple(points.Select(item => item.ImagePoint.Y).ToArray()),
                new HTuple(points.Select(item => item.WorldPoint.X).ToArray()),
                new HTuple(points.Select(item => item.WorldPoint.Y).ToArray()));
            HHomMat2D worldToImage = imageToWorld.HomMat2dInvert();

            var residuals = new List<VisionCalibrationResidual>(points.Length);
            double squaredErrorSum = 0;
            double maximumError = 0;
            foreach (VisionPointCorrespondence point in points)
            {
                double actualX = imageToWorld.AffineTransPoint2d(
                    point.ImagePoint.X,
                    point.ImagePoint.Y,
                    out double actualY);
                double dx = actualX - point.WorldPoint.X;
                double dy = actualY - point.WorldPoint.Y;
                double error = Math.Sqrt(dx * dx + dy * dy);
                squaredErrorSum += error * error;
                maximumError = Math.Max(maximumError, error);
                residuals.Add(new VisionCalibrationResidual(
                    point.ImagePoint,
                    point.WorldPoint,
                    new VisionPoint(actualX, actualY),
                    error));
            }

            var result = new PlanarCalibrationResult(
                ToVisionTransform(imageToWorld),
                ToVisionTransform(worldToImage),
                Math.Sqrt(squaredErrorSum / points.Length),
                maximumError,
                residuals);

            var overlays = new List<IVisionOverlayShape>(points.Length * 2);
            for (int i = 0; i < points.Length; i++)
            {
                var imgPt = points[i].ImagePoint;
                var worldPt = points[i].WorldPoint;
                string ptLabel = (i + 1 < CachedPointLabels.Length) ? CachedPointLabels[i + 1] : $"Pt {i + 1}";
                string ptPrefix = (i + 1 < CachedPointPrefixes.Length) ? CachedPointPrefixes[i + 1] : $"Pt {i + 1}: ";
                overlays.Add(new OverlayCircle(new VisionCircle(imgPt, 4.0), VisionColor.Green, 1.5, ptLabel));
                overlays.Add(new OverlayText(new VisionPoint(imgPt.X + 6, imgPt.Y - 6), $"{ptPrefix}({worldPt.X:F1}, {worldPt.Y:F1})", VisionColor.Yellow, 10));
            }

            stopwatch.Stop();
            return ValueTask.FromResult(
                VisionExecutionResult<PlanarCalibrationResult>.Success(
                    result,
                    stopwatch.Elapsed,
                    new Dictionary<string, string>
                    {
                        ["Backend"] = BackendId,
                        ["PointCount"] = points.Length.ToString()
                    },
                    overlays));
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return ValueTask.FromResult(
                VisionExecutionResult<PlanarCalibrationResult>.Failure(
                    "HALCON_CALIBRATION_FAILED",
                    ex.Message,
                    stopwatch.Elapsed));
        }
    }

    private static VisionTransform2D ToVisionTransform(HHomMat2D matrix)
    {
        // Reconstruct through transformed basis points to preserve shear as well.
        double originX = matrix.AffineTransPoint2d(0, 0, out double originY);
        double xAxisX = matrix.AffineTransPoint2d(1, 0, out double xAxisY);
        double yAxisX = matrix.AffineTransPoint2d(0, 1, out double yAxisY);

        return new VisionTransform2D(
            xAxisX - originX,
            yAxisX - originX,
            xAxisY - originY,
            yAxisY - originY,
            originX,
            originY);
    }

    private static void Validate(PlanarCalibrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Correspondences);
        if (request.Correspondences.Count < 3)
        {
            throw new ArgumentException("Planar affine calibration requires at least three point pairs.", nameof(request));
        }

        if (request.Correspondences.Any(item => !IsFinite(item.ImagePoint) || !IsFinite(item.WorldPoint)))
        {
            throw new ArgumentException("Calibration coordinates must be finite.", nameof(request));
        }
    }

    private static bool IsFinite(VisionPoint point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
