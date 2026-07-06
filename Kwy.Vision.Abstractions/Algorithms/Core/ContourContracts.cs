using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionEdgeFilter
{
    Canny,
    Deriche1,
    Deriche2,
    Shen,
    Lanser1,
    Lanser2,
    Mshen
}

public sealed record ContourDetectionRequest(
    IVisionImage Image,
    double Alpha,
    double LowThreshold,
    double HighThreshold,
    double MinimumLength,
    double MaximumLength,
    VisionEdgeFilter Filter = VisionEdgeFilter.Canny,
    IVisionRegion? SearchRegion = null,
    int MaximumCount = int.MaxValue);

public sealed record VisionDetectedContour(
    VisionContour Contour,
    double Length);

public sealed record ContourDetectionResult(IReadOnlyList<VisionDetectedContour> Contours);
