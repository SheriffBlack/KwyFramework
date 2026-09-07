using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconLineFittingAlgorithm
    : HalconVisionAlgorithm<LineFittingRequest, LineFittingResult>
{
    public const string Id = "LineFitting";

    public HalconLineFittingAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<LineFittingResult>> ExecuteAsync(
        LineFittingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        HalconFittingUtilities.ValidateFitting(
            request.Points,
            2,
            request.ClippingEndPoints,
            request.Iterations,
            request.ClippingFactor);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using HXLDCont contour = HalconFittingUtilities.CreateContour(request.Points);
            contour.FitLineContourXld(
                HalconFittingUtilities.ToHalconAlgorithm(request.Mode),
                -1,
                request.ClippingEndPoints,
                request.Iterations,
                request.ClippingFactor,
                out double rowBegin,
                out double columnBegin,
                out double rowEnd,
                out double columnEnd,
                out double normalRow,
                out double normalColumn,
                out double distance);

            var start = new VisionPoint(columnBegin, rowBegin);
            var end = new VisionPoint(columnEnd, rowEnd);
            (double meanResidual, double maxResidual) = HalconFittingUtilities.CalculateLineResiduals(
                request.Points,
                normalRow,
                normalColumn,
                distance);
            var result = new LineFittingResult(
                new VisionLine(start, end),
                start,
                end,
                new VisionPoint(normalColumn, normalRow),
                distance,
                meanResidual,
                maxResidual);
            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<LineFittingResult>.Success(
                result,
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["PointCount"] = request.Points.Count.ToString()
                },
                [new OverlayLine(result.Line, VisionColor.Green, 1.5, "Fitted Line")]));
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<LineFittingResult>.Failure(
                "HALCON_LINE_FITTING_FAILED",
                ex.Message,
                stopwatch.Elapsed));
        }
    }
}
