using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionEdgePolarity
{
    All,
    Positive,
    Negative
}

public enum VisionEdgeSelection
{
    All,
    First,
    Last
}

public sealed record EdgeMeasurementRequest(
    IVisionImage Image,
    VisionRotatedRectangle MeasureRegion,
    double Sigma,
    double Threshold,
    VisionEdgePolarity Polarity = VisionEdgePolarity.All,
    VisionEdgeSelection Selection = VisionEdgeSelection.All);

public sealed record VisionMeasuredEdge(
    VisionPoint Position,
    double Amplitude,
    double DistanceFromPrevious);

public sealed record EdgeMeasurementResult(IReadOnlyList<VisionMeasuredEdge> Edges);
