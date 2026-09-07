using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconCircleFittingAlgorithm
    : HalconVisionAlgorithm<CircleFittingRequest, CircleFittingResult>
{
    public const string Id = "CircleFitting";

    public HalconCircleFittingAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<CircleFittingResult>> ExecuteAsync(
        CircleFittingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        HalconFittingUtilities.ValidateFitting(
            request.Points,
            3,
            request.ClippingEndPoints,
            request.Iterations,
            request.ClippingFactor);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using HXLDCont contour = HalconFittingUtilities.CreateContour(request.Points);
            contour.FitCircleContourXld(
                HalconFittingUtilities.ToHalconAlgorithm(request.Mode),
                -1,
                0,
                request.ClippingEndPoints,
                request.Iterations,
                request.ClippingFactor,
                out double row,
                out double column,
                out double radius,
                out double startPhi,
                out double endPhi,
                out string pointOrder);

            var circle = new VisionCircle(new VisionPoint(column, row), radius);
            (double meanResidual, double maxResidual) = HalconFittingUtilities.CalculateCircleResiduals(
                request.Points,
                circle);
            var result = new CircleFittingResult(
                circle,
                startPhi,
                endPhi,
                pointOrder,
                meanResidual,
                maxResidual);
            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<CircleFittingResult>.Success(
                result,
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["PointCount"] = request.Points.Count.ToString()
                },
                [new OverlayCircle(circle, VisionColor.Green, 1.5, "Fitted Circle")]));
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<CircleFittingResult>.Failure(
                "HALCON_CIRCLE_FITTING_FAILED",
                ex.Message,
                stopwatch.Elapsed));
        }
    }
}
