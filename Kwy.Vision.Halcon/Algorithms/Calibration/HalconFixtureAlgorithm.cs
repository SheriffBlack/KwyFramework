using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

/// <summary>
/// Calculates the rigid coordinate transformation from a reference pose to the current pose using vector_angle_to_rigid.
/// </summary>
public sealed class HalconFixtureAlgorithm
    : HalconVisionAlgorithm<FixtureRequest, FixtureResult>
{
    public const string Id = "PositionCompensation";

    public HalconFixtureAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<FixtureResult>> ExecuteAsync(
        FixtureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var refPose = request.ReferencePose;
            var curPose = request.CurrentPose;

            var matrix = new HHomMat2D();
            matrix.VectorAngleToRigid(
                refPose.Y,
                refPose.X,
                refPose.AngleRadians,
                curPose.Y,
                curPose.X,
                curPose.AngleRadians);

            double originX = matrix.AffineTransPoint2d(0, 0, out double originY);
            double xAxisX = matrix.AffineTransPoint2d(1, 0, out double xAxisY);
            double yAxisX = matrix.AffineTransPoint2d(0, 1, out double yAxisY);

            var transform = new VisionTransform2D(
                xAxisX - originX,
                yAxisX - originX,
                xAxisY - originY,
                yAxisY - originY,
                originX,
                originY);

            // Preallocate 6 overlay elements
            var overlays = new List<IVisionOverlayShape>(6);

            // Draw reference pose coordinate system (Blue)
            var refPt = new VisionPoint(refPose.X, refPose.Y);
            var refX = new VisionPoint(refPose.X + 30 * Math.Cos(refPose.AngleRadians), refPose.Y + 30 * Math.Sin(refPose.AngleRadians));
            var refY = new VisionPoint(refPose.X - 30 * Math.Sin(refPose.AngleRadians), refPose.Y + 30 * Math.Cos(refPose.AngleRadians));

            overlays.Add(new OverlayCircle(new VisionCircle(refPt, 3.0), VisionColor.Blue, 1.5, "Ref Center"));
            overlays.Add(new OverlayLine(new VisionLine(refPt, refX), VisionColor.Blue, 1.5, "Ref X"));
            overlays.Add(new OverlayLine(new VisionLine(refPt, refY), VisionColor.Blue, 1.5, "Ref Y"));

            // Draw current pose coordinate system (Green)
            var curPt = new VisionPoint(curPose.X, curPose.Y);
            var curX = new VisionPoint(curPose.X + 30 * Math.Cos(curPose.AngleRadians), curPose.Y + 30 * Math.Sin(curPose.AngleRadians));
            var curY = new VisionPoint(curPose.X - 30 * Math.Sin(curPose.AngleRadians), curPose.Y + 30 * Math.Cos(curPose.AngleRadians));

            overlays.Add(new OverlayCircle(new VisionCircle(curPt, 3.0), VisionColor.Green, 1.5, "Cur Center"));
            overlays.Add(new OverlayLine(new VisionLine(curPt, curX), VisionColor.Green, 1.5, "Cur X"));
            overlays.Add(new OverlayLine(new VisionLine(curPt, curY), VisionColor.Green, 1.5, "Cur Y"));

            stopwatch.Stop();
            var result = new FixtureResult(transform);
            return ValueTask.FromResult(
                VisionExecutionResult<FixtureResult>.Success(
                    result,
                    stopwatch.Elapsed,
                    new Dictionary<string, string>
                    {
                        ["Backend"] = BackendId,
                        ["TranslationX"] = (curPose.X - refPose.X).ToString("F3"),
                        ["TranslationY"] = (curPose.Y - refPose.Y).ToString("F3"),
                        ["RotationAngleDeg"] = ((curPose.AngleRadians - refPose.AngleRadians) * 180.0 / Math.PI).ToString("F3")
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
                VisionExecutionResult<FixtureResult>.Failure(
                    "HALCON_OPERATOR_FAILED",
                    ex.Message,
                    stopwatch.Elapsed));
        }
    }
}
