using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionDistanceTargetType
{
    Point,
    Line,
    Circle
}

public sealed record VisionDistanceTarget(
    VisionDistanceTargetType Type,
    VisionPoint? Point = null,
    VisionLine? Line = null,
    VisionCircle? Circle = null)
{
    public static VisionDistanceTarget FromPoint(VisionPoint point) => new(VisionDistanceTargetType.Point, Point: point);

    public static VisionDistanceTarget FromLine(VisionLine line) => new(VisionDistanceTargetType.Line, Line: line);

    public static VisionDistanceTarget FromCircle(VisionCircle circle) => new(VisionDistanceTargetType.Circle, Circle: circle);
}

public sealed record DistanceMeasurementRequest(
    VisionDistanceTarget First,
    VisionDistanceTarget Second);

public sealed record DistanceMeasurementResult(
    double Distance,
    VisionPoint FirstClosestPoint,
    VisionPoint SecondClosestPoint);
