using System.Diagnostics;
using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.Halcon.Images;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconEdgeMeasurementAlgorithm
    : HalconVisionAlgorithm<EdgeMeasurementRequest, EdgeMeasurementResult>
{
    public const string Id = "EdgeMeasurement";

    private static readonly string[] CachedEdgeLabels = Enumerable.Range(0, 101).Select(i => $"E{i}").ToArray();
    private static readonly string[] CachedEdgeNames = Enumerable.Range(0, 101).Select(i => $"Edge {i}").ToArray();

    private readonly HalconVisionImageConverter converter;

    public HalconEdgeMeasurementAlgorithm(HalconVisionImageConverter converter)
        : base(Id)
    {
        this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public override async ValueTask<VisionExecutionResult<EdgeMeasurementResult>> ExecuteAsync(
        EdgeMeasurementRequest request,
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
            cancellationToken.ThrowIfCancellationRequested();

            VisionRotatedRectangle region = request.MeasureRegion;
            IReadOnlyList<VisionMeasuredEdge> edges = HalconMeasurementUtilities.MeasureEdges(
                lease.Image,
                request.Image.Width,
                request.Image.Height,
                region,
                request.Sigma,
                request.Threshold,
                request.Polarity,
                request.Selection);
            var overlays = new List<IVisionOverlayShape>(edges.Count * 3 + 1);

            // Calculate caliper corners for overlay visualization
            double cos = Math.Cos(region.AngleRadians);
            double sin = Math.Sin(region.AngleRadians);
            double halfW = region.Width / 2.0;
            double halfH = region.Height / 2.0;

            var corners = new[]
            {
                new VisionPoint(region.Center.X - halfW * cos + halfH * sin, region.Center.Y - halfW * sin - halfH * cos),
                new VisionPoint(region.Center.X + halfW * cos + halfH * sin, region.Center.Y + halfW * sin - halfH * cos),
                new VisionPoint(region.Center.X + halfW * cos - halfH * sin, region.Center.Y + halfW * sin + halfH * cos),
                new VisionPoint(region.Center.X - halfW * cos - halfH * sin, region.Center.Y - halfW * sin + halfH * cos)
            };
            overlays.Add(new OverlayContour(new VisionContour(corners, isClosed: true), VisionColor.Cyan, 1.0, "Caliper Region"));

            for (int i = 0; i < edges.Count; i++)
            {
                double edgeX = edges[i].Position.X;
                double edgeY = edges[i].Position.Y;
                var edgePos = new VisionPoint(edgeX, edgeY);

                // Draw a tick line perpendicular to measurement direction (along the height of the caliper)
                double tickHalfLength = Math.Min(6.0, halfH);
                var tickStart = new VisionPoint(edgeX - tickHalfLength * sin, edgeY + tickHalfLength * cos);
                var tickEnd = new VisionPoint(edgeX + tickHalfLength * sin, edgeY - tickHalfLength * cos);

                string edgeName = (i + 1 < CachedEdgeNames.Length) ? CachedEdgeNames[i + 1] : $"Edge {i + 1}";
                string labelText = (i + 1 < CachedEdgeLabels.Length) ? CachedEdgeLabels[i + 1] : $"E{i + 1}";

                overlays.Add(new OverlayLine(new VisionLine(tickStart, tickEnd), VisionColor.Green, 2.0, edgeName));
                overlays.Add(new OverlayCircle(new VisionCircle(edgePos, 2.0), VisionColor.Red, 1.0));
                overlays.Add(new OverlayText(new VisionPoint(edgeX + 5, edgeY - 5), labelText, VisionColor.Yellow, 10));
            }

            stopwatch.Stop();
            return VisionExecutionResult<EdgeMeasurementResult>.Success(
                new EdgeMeasurementResult(edges),
                stopwatch.Elapsed,
                new Dictionary<string, string>
                {
                    ["Backend"] = BackendId,
                    ["EdgeCount"] = edges.Count.ToString()
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
            return VisionExecutionResult<EdgeMeasurementResult>.Failure(
                "HALCON_MEASUREMENT_FAILED",
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    private static void Validate(EdgeMeasurementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Image);
        HalconMeasurementUtilities.ValidateMeasurement(
            request.MeasureRegion,
            request.Sigma,
            request.Threshold);
    }
}
