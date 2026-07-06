using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum GeometryMeasurementOperation
{
    Angle,
    Intersection,
    Parallelism,
    Perpendicularity,
    Concentricity
}

public sealed record GeometryMeasurementRequest(
    GeometryMeasurementOperation Operation,
    VisionLine? FirstLine = null,
    VisionLine? SecondLine = null,
    VisionCircle? FirstCircle = null,
    VisionCircle? SecondCircle = null);

public sealed record GeometryMeasurementResult(
    GeometryMeasurementOperation Operation,
    double Value,
    string Unit,
    VisionPoint? Point = null,
    bool HasIntersection = false);
