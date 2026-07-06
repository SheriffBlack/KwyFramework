using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionMetrologyEdgePolarity
{
    All,
    Positive,
    Negative
}

public sealed record LineMetrologyRequest(
    IVisionImage Image,
    VisionLine ApproximateLine,
    double MeasureLength1,
    double MeasureLength2,
    double MeasureSigma,
    double MeasureThreshold,
    VisionMetrologyEdgePolarity EdgePolarity = VisionMetrologyEdgePolarity.All,
    int MinimumScore = 30);

public sealed record LineMetrologyResult(
    VisionLine Line,
    double Score,
    IReadOnlyList<VisionPoint> MeasuredPoints);

public sealed record CircleMetrologyRequest(
    IVisionImage Image,
    VisionCircle ApproximateCircle,
    double MeasureLength1,
    double MeasureLength2,
    double MeasureSigma,
    double MeasureThreshold,
    VisionMetrologyEdgePolarity EdgePolarity = VisionMetrologyEdgePolarity.All,
    int MinimumScore = 30);

public sealed record CircleMetrologyResult(
    VisionCircle Circle,
    double Score,
    IReadOnlyList<VisionPoint> MeasuredPoints);
