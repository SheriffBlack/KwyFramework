using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record ShapeMatchingRequest(
    IVisionImage Image,
    string TemplateId,
    double AngleStartRadians,
    double AngleExtentRadians,
    double MinimumScore = 0.5,
    int MaximumMatches = 1,
    double MaximumOverlap = 0.5,
    IVisionRegion? SearchRegion = null);

public sealed record ShapeMatchingResult(IReadOnlyList<VisionShapeMatch> Matches);

public sealed record VisionShapeMatch(
    VisionPose2D Pose,
    double Score);
