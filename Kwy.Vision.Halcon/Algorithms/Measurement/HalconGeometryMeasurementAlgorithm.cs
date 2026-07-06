using System.Diagnostics;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconGeometryMeasurementAlgorithm
    : HalconVisionAlgorithm<GeometryMeasurementRequest, GeometryMeasurementResult>
{
    public const string Id = "GeometryMeasurement";

    public HalconGeometryMeasurementAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<GeometryMeasurementResult>> ExecuteAsync(
        GeometryMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        GeometryMeasurementResult result = request.Operation switch
        {
            GeometryMeasurementOperation.Angle => MeasureAngle(RequireLine(request.FirstLine), RequireLine(request.SecondLine)),
            GeometryMeasurementOperation.Intersection => MeasureIntersection(RequireLine(request.FirstLine), RequireLine(request.SecondLine)),
            GeometryMeasurementOperation.Parallelism => MeasureParallelism(RequireLine(request.FirstLine), RequireLine(request.SecondLine)),
            GeometryMeasurementOperation.Perpendicularity => MeasurePerpendicularity(RequireLine(request.FirstLine), RequireLine(request.SecondLine)),
            GeometryMeasurementOperation.Concentricity => MeasureConcentricity(RequireCircle(request.FirstCircle), RequireCircle(request.SecondCircle)),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Operation), request.Operation, null)
        };
        stopwatch.Stop();

        return ValueTask.FromResult(VisionExecutionResult<GeometryMeasurementResult>.Success(
            result,
            stopwatch.Elapsed,
            new Dictionary<string, string>
            {
                ["Backend"] = BackendId,
                ["Operation"] = request.Operation.ToString()
            }));
    }

    private static GeometryMeasurementResult MeasureAngle(VisionLine first, VisionLine second)
    {
        double angle = Math.Abs(NormalizeAngle(LineAngle(first) - LineAngle(second)));
        angle = Math.Min(angle, Math.PI - angle);
        return new GeometryMeasurementResult(GeometryMeasurementOperation.Angle, angle, "rad");
    }

    private static GeometryMeasurementResult MeasureIntersection(VisionLine first, VisionLine second)
    {
        bool hasIntersection = TryIntersect(first, second, out VisionPoint point);
        return new GeometryMeasurementResult(
            GeometryMeasurementOperation.Intersection,
            hasIntersection ? 1 : 0,
            "bool",
            hasIntersection ? point : null,
            hasIntersection);
    }

    private static GeometryMeasurementResult MeasureParallelism(VisionLine first, VisionLine second)
    {
        double angle = MeasureAngle(first, second).Value;
        return new GeometryMeasurementResult(GeometryMeasurementOperation.Parallelism, angle, "rad");
    }

    private static GeometryMeasurementResult MeasurePerpendicularity(VisionLine first, VisionLine second)
    {
        double angle = MeasureAngle(first, second).Value;
        return new GeometryMeasurementResult(
            GeometryMeasurementOperation.Perpendicularity,
            Math.Abs(Math.PI / 2 - angle),
            "rad");
    }

    private static GeometryMeasurementResult MeasureConcentricity(VisionCircle first, VisionCircle second)
    {
        double dx = first.Center.X - second.Center.X;
        double dy = first.Center.Y - second.Center.Y;
        return new GeometryMeasurementResult(
            GeometryMeasurementOperation.Concentricity,
            Math.Sqrt(dx * dx + dy * dy),
            "px");
    }

    private static double LineAngle(VisionLine line)
        => Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X);

    private static double NormalizeAngle(double angle)
    {
        while (angle < 0)
        {
            angle += Math.PI;
        }

        while (angle >= Math.PI)
        {
            angle -= Math.PI;
        }

        return angle;
    }

    private static bool TryIntersect(VisionLine first, VisionLine second, out VisionPoint point)
    {
        double x1 = first.Start.X;
        double y1 = first.Start.Y;
        double x2 = first.End.X;
        double y2 = first.End.Y;
        double x3 = second.Start.X;
        double y3 = second.Start.Y;
        double x4 = second.End.X;
        double y4 = second.End.Y;
        double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denominator) < 1e-12)
        {
            point = default;
            return false;
        }

        double px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denominator;
        double py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denominator;
        point = new VisionPoint(px, py);
        return true;
    }

    private static VisionLine RequireLine(VisionLine? line)
        => line ?? throw new ArgumentException("Line input is required.");

    private static VisionCircle RequireCircle(VisionCircle? circle)
        => circle ?? throw new ArgumentException("Circle input is required.");
}
