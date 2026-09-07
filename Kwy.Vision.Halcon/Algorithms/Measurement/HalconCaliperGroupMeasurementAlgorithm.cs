using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconCaliperGroupMeasurementAlgorithm
    : HalconVisionAlgorithm<CaliperGroupMeasurementRequest, CaliperGroupMeasurementResult>
{
    public const string Id = "CaliperGroupMeasurement";

    private readonly HalconVisionImageConverter converter;

    public HalconCaliperGroupMeasurementAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<CaliperGroupMeasurementResult>> ExecuteAsync(
        CaliperGroupMeasurementRequest request,
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

            var results = new List<CaliperMeasurementResult>(request.Calipers.Count);
            var overlays = new List<IVisionOverlayShape>(request.Calipers.Count);
            foreach (CaliperDefinition caliper in request.Calipers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<VisionMeasuredEdge> edges = HalconMeasurementUtilities.MeasureEdges(
                    lease.Image,
                    request.Image.Width,
                    request.Image.Height,
                    caliper.MeasureRegion,
                    request.Sigma,
                    request.Threshold,
                    request.Polarity,
                    request.Selection);
                results.Add(new CaliperMeasurementResult(caliper.Name, caliper.MeasureRegion, edges));
                overlays.Add(new OverlayContour(
                    CreateCaliperContour(caliper.MeasureRegion),
                    VisionColor.Cyan,
                    1.0,
                    caliper.Name));
            }

            stopwatch.Stop();
            return VisionExecutionResult<CaliperGroupMeasurementResult>.Success(
                new CaliperGroupMeasurementResult(results),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["CaliperCount"] = results.Count.ToString()
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
            return VisionExecutionResult<CaliperGroupMeasurementResult>.Failure(
                "HALCON_CALIPER_GROUP_MEASUREMENT_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static VisionContour CreateCaliperContour(VisionRotatedRectangle region)
    {
        double cos = Math.Cos(region.AngleRadians);
        double sin = Math.Sin(region.AngleRadians);
        double halfW = region.Width / 2.0;
        double halfH = region.Height / 2.0;

        return new VisionContour(
            [
                new(region.Center.X - halfW * cos + halfH * sin, region.Center.Y - halfW * sin - halfH * cos),
                new(region.Center.X + halfW * cos + halfH * sin, region.Center.Y + halfW * sin - halfH * cos),
                new(region.Center.X + halfW * cos - halfH * sin, region.Center.Y + halfW * sin + halfH * cos),
                new(region.Center.X - halfW * cos - halfH * sin, region.Center.Y - halfW * sin + halfH * cos)
            ],
            isClosed: true);
    }

    private static void Validate(CaliperGroupMeasurementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        ArgumentNullException.ThrowIfNull(request.Calipers);
        if (request.Calipers.Count == 0)
        {
            throw new ArgumentException("At least one caliper is required.", nameof(request));
        }

        foreach (CaliperDefinition caliper in request.Calipers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caliper.Name);
            HalconMeasurementUtilities.ValidateMeasurement(
                caliper.MeasureRegion,
                request.Sigma,
                request.Threshold);
        }
    }
}
