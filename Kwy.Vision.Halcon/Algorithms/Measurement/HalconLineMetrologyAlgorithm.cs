using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconLineMetrologyAlgorithm
    : HalconVisionAlgorithm<LineMetrologyRequest, LineMetrologyResult>
{
    public const string Id = "LineMetrology";

    private readonly HalconVisionImageConverter converter;

    public HalconLineMetrologyAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<LineMetrologyResult>> ExecuteAsync(
        LineMetrologyRequest request,
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
            using var model = new HMetrologyModel();
            model.CreateMetrologyModel();
            model.SetMetrologyModelImageSize(request.Image.Width, request.Image.Height);
            int index = model.AddMetrologyObjectLineMeasure(
                request.ApproximateLine.Start.Y,
                request.ApproximateLine.Start.X,
                request.ApproximateLine.End.Y,
                request.ApproximateLine.End.X,
                request.MeasureLength1,
                request.MeasureLength2,
                1,
                request.MeasureThreshold,
                new HTuple(),
                new HTuple());
            HalconMetrologyUtilities.SetCommonObjectParameters(
                model,
                index,
                request.MeasureSigma,
                request.MeasureThreshold,
                request.EdgePolarity,
                request.MinimumScore);
            model.ApplyMetrologyModel(lease.Image);
            HTuple parameters = model.GetMetrologyObjectResult(index, "all", "result_type", "all_param");
            if (parameters.Length < 4)
            {
                return Failure(stopwatch, "No line metrology result was found.");
            }

            var line = new VisionLine(
                new VisionPoint(parameters[1].D, parameters[0].D),
                new VisionPoint(parameters[3].D, parameters[2].D));
            IReadOnlyList<VisionPoint> points = HalconMetrologyUtilities.GetMeasuredPoints(model, index);
            var result = new LineMetrologyResult(line, 1, points);
            stopwatch.Stop();
            return VisionExecutionResult<LineMetrologyResult>.Success(
                result,
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["PointCount"] = points.Count.ToString()
                },
                [new OverlayLine(line, VisionColor.Green, 1.5, "Metrology Line")]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return VisionExecutionResult<LineMetrologyResult>.Failure(
                "HALCON_LINE_METROLOGY_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static VisionExecutionResult<LineMetrologyResult> Failure(Stopwatch stopwatch, string message)
    {
        stopwatch.Stop();
        return VisionExecutionResult<LineMetrologyResult>.Failure(
            "HALCON_LINE_METROLOGY_NOT_FOUND",
            message,
            stopwatch.Elapsed);
    }

    private static void Validate(LineMetrologyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        HalconMetrologyUtilities.ValidateCommon(
            request.MeasureLength1,
            request.MeasureLength2,
            request.MeasureSigma,
            request.MeasureThreshold,
            request.MinimumScore);
    }
}
