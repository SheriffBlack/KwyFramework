using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

/// <summary>
/// Calculates the rotation center by fitting a circle to a list of points representing the rotation path.
/// </summary>
public sealed class HalconRotationCenterAlgorithm
    : HalconVisionAlgorithm<RotationCenterCalibrationRequest, RotationCenterCalibrationResult>
{
    public const string Id = "RotationCenterCalibration";

    private static readonly string[] CachedPointLabels = Enumerable.Range(0, 101).Select(i => $"Pt {i}").ToArray();

    public HalconRotationCenterAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<RotationCenterCalibrationResult>> ExecuteAsync(
        RotationCenterCalibrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.RotationPoints == null || request.RotationPoints.Count < 3)
        {
            throw new ArgumentException("At least 3 points are required to calculate the rotation center.", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var points = request.RotationPoints;
            double[] rowsArray = points.Select(p => p.Y).ToArray();
            double[] colsArray = points.Select(p => p.X).ToArray();

            using var contour = new HXLDCont();
            contour.GenContourPolygonXld(new HTuple(rowsArray), new HTuple(colsArray));

            contour.FitCircleContourXld(
                "algebraic",
                -1,
                0,
                0,
                3,
                2,
                out HTuple rowCenter,
                out HTuple colCenter,
                out HTuple radius,
                out HTuple startPhi,
                out HTuple endPhi,
                out HTuple pointOrder);

            double cx = colCenter.D;
            double cy = rowCenter.D;
            double r = radius.D;

            // Calculate residuals (Root Mean Square Error of the fit)
            double sumSqError = 0;
            foreach (var pt in points)
            {
                double dx = pt.X - cx;
                double dy = pt.Y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double err = dist - r;
                sumSqError += err * err;
            }
            double rmse = Math.Sqrt(sumSqError / points.Count);

            var pixelCenter = new VisionPoint(cx, cy);
            VisionPoint? worldCenter = null;
            if (request.ImageToWorld.HasValue)
            {
                worldCenter = request.ImageToWorld.Value.Transform(pixelCenter);
            }

            // Create overlays
            var overlays = new List<IVisionOverlayShape>(points.Count + 2);

            // 1. Draw input points
            for (int i = 0; i < points.Count; i++)
            {
                string label = (i + 1 < CachedPointLabels.Length) ? CachedPointLabels[i + 1] : $"Pt {i + 1}";
                overlays.Add(new OverlayCircle(new VisionCircle(points[i], 3.0), VisionColor.Yellow, 1.0, label));
            }

            // 2. Draw fitted circle
            overlays.Add(new OverlayCircle(new VisionCircle(pixelCenter, r), VisionColor.Green, 1.5, "Rotation Path Fit"));

            // 3. Draw rotation center
            overlays.Add(new OverlayCircle(new VisionCircle(pixelCenter, 5.0), VisionColor.Red, 2.0, "Rotation Center"));

            stopwatch.Stop();
            var result = new RotationCenterCalibrationResult(pixelCenter, r, rmse, worldCenter);
            return ValueTask.FromResult(
                VisionExecutionResult<RotationCenterCalibrationResult>.Success(
                    result,
                    stopwatch.Elapsed,
                    new Dictionary<string, string>
                    {
                        ["Backend"] = BackendId,
                        ["PointCount"] = points.Count.ToString(),
                        ["ResidualPixels"] = rmse.ToString("F3"),
                        ["RadiusPixels"] = r.ToString("F3")
                    },
                    overlays));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return ValueTask.FromResult(
                VisionExecutionResult<RotationCenterCalibrationResult>.Failure(
                    "HALCON_OPERATOR_FAILED",
                    ex.Message,
                    stopwatch.Elapsed));
        }
    }
}
