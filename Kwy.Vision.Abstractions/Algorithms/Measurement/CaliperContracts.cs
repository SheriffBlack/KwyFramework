using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record CaliperDefinition(
    string Name,
    VisionRotatedRectangle MeasureRegion);

public sealed record CaliperGroupMeasurementRequest(
    IVisionImage Image,
    IReadOnlyList<CaliperDefinition> Calipers,
    double Sigma,
    double Threshold,
    VisionEdgePolarity Polarity = VisionEdgePolarity.All,
    VisionEdgeSelection Selection = VisionEdgeSelection.All);

public sealed record CaliperMeasurementResult(
    string Name,
    VisionRotatedRectangle MeasureRegion,
    IReadOnlyList<VisionMeasuredEdge> Edges);

public sealed record CaliperGroupMeasurementResult(
    IReadOnlyList<CaliperMeasurementResult> Calipers);
