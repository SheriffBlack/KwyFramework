using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconContourFittingAlgorithm
    : HalconVisionAlgorithm<ContourFittingRequest, ContourFittingResult>
{
    public const string Id = "ContourFitting";

    public HalconContourFittingAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<ContourFittingResult>> ExecuteAsync(
        ContourFittingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using HXLDCont contour = HalconFittingUtilities.CreateContour(request.Contour.Points);
            ContourFittingResult result = request.Shape switch
            {
                VisionContourFitShape.Line => FitLine(request, contour),
                VisionContourFitShape.Circle => FitCircle(request, contour),
                VisionContourFitShape.RotatedRectangle => FitRotatedRectangle(request, contour),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Shape), request.Shape, null)
            };

            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<ContourFittingResult>.Success(
                result,
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["Shape"] = request.Shape.ToString(),
                    ["PointCount"] = request.Contour.Points.Count.ToString()
                },
                CreateOverlay(result)));
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return ValueTask.FromResult(VisionExecutionResult<ContourFittingResult>.Failure(
                "HALCON_CONTOUR_FITTING_FAILED",
                ex.Message,
                stopwatch.Elapsed));
        }
    }

    private static ContourFittingResult FitLine(ContourFittingRequest request, HXLDCont contour)
    {
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

        var line = new VisionLine(
            new VisionPoint(columnBegin, rowBegin),
            new VisionPoint(columnEnd, rowEnd));
        (double mean, double max) = HalconFittingUtilities.CalculateLineResiduals(
            request.Contour.Points,
            normalRow,
            normalColumn,
            distance);
        return new ContourFittingResult(VisionContourFitShape.Line, Line: line, MeanResidual: mean, MaxResidual: max);
    }

    private static ContourFittingResult FitCircle(ContourFittingRequest request, HXLDCont contour)
    {
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
        _ = startPhi;
        _ = endPhi;
        _ = pointOrder;

        var circle = new VisionCircle(new VisionPoint(column, row), radius);
        (double mean, double max) = HalconFittingUtilities.CalculateCircleResiduals(request.Contour.Points, circle);
        return new ContourFittingResult(VisionContourFitShape.Circle, Circle: circle, MeanResidual: mean, MaxResidual: max);
    }

    private static ContourFittingResult FitRotatedRectangle(ContourFittingRequest request, HXLDCont contour)
    {
        contour.FitRectangle2ContourXld(
            HalconFittingUtilities.ToHalconAlgorithm(request.Mode),
            -1,
            0,
            request.ClippingEndPoints,
            request.Iterations,
            request.ClippingFactor,
            out double row,
            out double column,
            out double phi,
            out double length1,
            out double length2,
            out string pointOrder);
        _ = pointOrder;

        var rectangle = new VisionRotatedRectangle(
            new VisionPoint(column, row),
            length1 * 2,
            length2 * 2,
            phi);
        return new ContourFittingResult(VisionContourFitShape.RotatedRectangle, RotatedRectangle: rectangle);
    }

    private static IReadOnlyList<IVisionOverlayShape> CreateOverlay(ContourFittingResult result)
        => result.Shape switch
        {
            VisionContourFitShape.Line when result.Line.HasValue =>
            [
                new OverlayLine(result.Line.Value, VisionColor.Green, 1.5, "Fitted Line")
            ],
            VisionContourFitShape.Circle when result.Circle.HasValue =>
            [
                new OverlayCircle(result.Circle.Value, VisionColor.Green, 1.5, "Fitted Circle")
            ],
            _ => Array.Empty<IVisionOverlayShape>()
        };

    private static void Validate(ContourFittingRequest request)
    {
        HalconFittingUtilities.ValidateFitting(
            request.Contour.Points,
            request.Shape == VisionContourFitShape.Line ? 2 : 3,
            request.ClippingEndPoints,
            request.Iterations,
            request.ClippingFactor);
    }
}
