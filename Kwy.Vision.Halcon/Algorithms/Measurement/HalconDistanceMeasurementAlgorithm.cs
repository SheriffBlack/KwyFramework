using System.Diagnostics;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Halcon.Algorithms;

public sealed class HalconDistanceMeasurementAlgorithm
    : HalconVisionAlgorithm<DistanceMeasurementRequest, DistanceMeasurementResult>
{
    public const string Id = "DistanceMeasurement";

    public HalconDistanceMeasurementAlgorithm()
        : base(Id)
    {
    }

    public override ValueTask<VisionExecutionResult<DistanceMeasurementResult>> ExecuteAsync(
        DistanceMeasurementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        DistanceMeasurementResult result = Measure(request.First, request.Second);
        stopwatch.Stop();

        return ValueTask.FromResult(VisionExecutionResult<DistanceMeasurementResult>.Success(
            result,
            stopwatch.Elapsed,
            new Dictionary<string, string> { ["Backend"] = BackendId },
            [
                new OverlayLine(
                    new VisionLine(result.FirstClosestPoint, result.SecondClosestPoint),
                    VisionColor.Cyan,
                    1.5,
                    "Distance")
            ]));
    }

    private static DistanceMeasurementResult Measure(
        VisionDistanceTarget first,
        VisionDistanceTarget second)
    {
        ValidateTarget(first, nameof(first));
        ValidateTarget(second, nameof(second));

        return (first.Type, second.Type) switch
        {
            (VisionDistanceTargetType.Point, VisionDistanceTargetType.Point) =>
                FromPoints(first.Point!.Value, second.Point!.Value),
            (VisionDistanceTargetType.Point, VisionDistanceTargetType.Line) =>
                PointToLine(first.Point!.Value, second.Line!.Value),
            (VisionDistanceTargetType.Line, VisionDistanceTargetType.Point) =>
                Swap(PointToLine(second.Point!.Value, first.Line!.Value)),
            (VisionDistanceTargetType.Point, VisionDistanceTargetType.Circle) =>
                PointToCircle(first.Point!.Value, second.Circle!.Value),
            (VisionDistanceTargetType.Circle, VisionDistanceTargetType.Point) =>
                Swap(PointToCircle(second.Point!.Value, first.Circle!.Value)),
            (VisionDistanceTargetType.Line, VisionDistanceTargetType.Line) =>
                LineToLine(first.Line!.Value, second.Line!.Value),
            (VisionDistanceTargetType.Line, VisionDistanceTargetType.Circle) =>
                LineToCircle(first.Line!.Value, second.Circle!.Value),
            (VisionDistanceTargetType.Circle, VisionDistanceTargetType.Line) =>
                Swap(LineToCircle(second.Line!.Value, first.Circle!.Value)),
            (VisionDistanceTargetType.Circle, VisionDistanceTargetType.Circle) =>
                CircleToCircle(first.Circle!.Value, second.Circle!.Value),
            _ => throw new NotSupportedException("Unsupported distance target combination.")
        };
    }

    private static DistanceMeasurementResult FromPoints(VisionPoint first, VisionPoint second)
        => new(Distance(first, second), first, second);

    private static DistanceMeasurementResult PointToLine(VisionPoint point, VisionLine line)
    {
        VisionPoint projected = ProjectPointToLine(point, line);
        return new(Distance(point, projected), point, projected);
    }

    private static DistanceMeasurementResult PointToCircle(VisionPoint point, VisionCircle circle)
    {
        VisionPoint circlePoint = ClosestPointOnCircle(point, circle);
        return new(Distance(point, circlePoint), point, circlePoint);
    }

    private static DistanceMeasurementResult LineToLine(VisionLine first, VisionLine second)
    {
        if (TryIntersectLines(first, second, out VisionPoint intersection))
        {
            return new(0, intersection, intersection);
        }

        DistanceMeasurementResult a = PointToLine(first.Start, second);
        DistanceMeasurementResult b = PointToLine(second.Start, first);
        return a.Distance <= b.Distance
            ? a
            : Swap(b);
    }

    private static DistanceMeasurementResult LineToCircle(VisionLine line, VisionCircle circle)
    {
        VisionPoint projectedCenter = ProjectPointToLine(circle.Center, line);
        double centerDistance = Distance(circle.Center, projectedCenter);
        VisionPoint circlePoint = centerDistance == 0
            ? ClosestPointOnCircle(line.Start, circle)
            : MoveFromTo(circle.Center, projectedCenter, circle.Radius);
        double distance = Math.Max(0, centerDistance - circle.Radius);
        return new(distance, projectedCenter, circlePoint);
    }

    private static DistanceMeasurementResult CircleToCircle(VisionCircle first, VisionCircle second)
    {
        double centerDistance = Distance(first.Center, second.Center);
        if (centerDistance == 0)
        {
            var coincidentFirstPoint = new VisionPoint(first.Center.X + first.Radius, first.Center.Y);
            var coincidentSecondPoint = new VisionPoint(second.Center.X + second.Radius, second.Center.Y);
            return new(Math.Abs(first.Radius - second.Radius), coincidentFirstPoint, coincidentSecondPoint);
        }

        VisionPoint firstPoint = MoveFromTo(first.Center, second.Center, first.Radius);
        VisionPoint secondPoint = MoveFromTo(second.Center, first.Center, second.Radius);
        return new(Math.Max(0, centerDistance - first.Radius - second.Radius), firstPoint, secondPoint);
    }

    private static DistanceMeasurementResult Swap(DistanceMeasurementResult result)
        => new(result.Distance, result.SecondClosestPoint, result.FirstClosestPoint);

    private static VisionPoint ProjectPointToLine(VisionPoint point, VisionLine line)
    {
        double dx = line.End.X - line.Start.X;
        double dy = line.End.Y - line.Start.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0)
        {
            throw new ArgumentException("Line target cannot have identical start and end points.", nameof(line));
        }

        double t = ((point.X - line.Start.X) * dx + (point.Y - line.Start.Y) * dy) / lengthSquared;
        return new VisionPoint(line.Start.X + t * dx, line.Start.Y + t * dy);
    }

    private static bool TryIntersectLines(VisionLine first, VisionLine second, out VisionPoint intersection)
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
            intersection = default;
            return false;
        }

        double px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denominator;
        double py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denominator;
        intersection = new VisionPoint(px, py);
        return true;
    }

    private static VisionPoint ClosestPointOnCircle(VisionPoint point, VisionCircle circle)
        => MoveFromTo(circle.Center, point, circle.Radius);

    private static VisionPoint MoveFromTo(VisionPoint from, VisionPoint to, double distance)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0)
        {
            return new VisionPoint(from.X + distance, from.Y);
        }

        return new VisionPoint(from.X + dx / length * distance, from.Y + dy / length * distance);
    }

    private static double Distance(VisionPoint first, VisionPoint second)
    {
        double dx = first.X - second.X;
        double dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void ValidateTarget(VisionDistanceTarget target, string parameterName)
    {
        switch (target.Type)
        {
            case VisionDistanceTargetType.Point when target.Point.HasValue:
                ValidatePoint(target.Point.Value, parameterName);
                break;
            case VisionDistanceTargetType.Line when target.Line.HasValue:
                ValidatePoint(target.Line.Value.Start, parameterName);
                ValidatePoint(target.Line.Value.End, parameterName);
                break;
            case VisionDistanceTargetType.Circle when target.Circle.HasValue:
                ValidatePoint(target.Circle.Value.Center, parameterName);
                if (!double.IsFinite(target.Circle.Value.Radius) || target.Circle.Value.Radius < 0)
                {
                    throw new ArgumentOutOfRangeException(parameterName, target.Circle.Value.Radius, "Circle radius must be finite and non-negative.");
                }
                break;
            default:
                throw new ArgumentException("Distance target payload does not match its type.", parameterName);
        }
    }

    private static void ValidatePoint(VisionPoint point, string parameterName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentException("Distance target coordinates must be finite.", parameterName);
        }
    }
}
