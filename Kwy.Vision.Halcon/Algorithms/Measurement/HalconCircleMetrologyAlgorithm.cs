using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconCircleMetrologyAlgorithm
    : HalconVisionAlgorithm<CircleMetrologyRequest, CircleMetrologyResult>
{
    public const string Id = "CircleMetrology";

    private readonly HalconVisionImageConverter converter;

    public HalconCircleMetrologyAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<CircleMetrologyResult>> ExecuteAsync(
        CircleMetrologyRequest request,
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
            int index = model.AddMetrologyObjectCircleMeasure(
                request.ApproximateCircle.Center.Y,
                request.ApproximateCircle.Center.X,
                request.ApproximateCircle.Radius,
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
            if (parameters.Length < 3)
            {
                return Failure(stopwatch, "No circle metrology result was found.");
            }

            var circle = new VisionCircle(
                new VisionPoint(parameters[1].D, parameters[0].D),
                parameters[2].D);
            IReadOnlyList<VisionPoint> points = HalconMetrologyUtilities.GetMeasuredPoints(model, index);
            var result = new CircleMetrologyResult(circle, 1, points);
            stopwatch.Stop();
            return VisionExecutionResult<CircleMetrologyResult>.Success(
                result,
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["PointCount"] = points.Count.ToString()
                },
                [new OverlayCircle(circle, VisionColor.Green, 1.5, "Metrology Circle")]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HOperatorException ex)
        {
            stopwatch.Stop();
            return VisionExecutionResult<CircleMetrologyResult>.Failure(
                "HALCON_CIRCLE_METROLOGY_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static VisionExecutionResult<CircleMetrologyResult> Failure(Stopwatch stopwatch, string message)
    {
        stopwatch.Stop();
        return VisionExecutionResult<CircleMetrologyResult>.Failure(
            "HALCON_CIRCLE_METROLOGY_NOT_FOUND",
            message,
            stopwatch.Elapsed);
    }

    private static void Validate(CircleMetrologyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        if (!double.IsFinite(request.ApproximateCircle.Radius) || request.ApproximateCircle.Radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ApproximateCircle.Radius));
        }

        HalconMetrologyUtilities.ValidateCommon(
            request.MeasureLength1,
            request.MeasureLength2,
            request.MeasureSigma,
            request.MeasureThreshold,
            request.MinimumScore);
    }
}
