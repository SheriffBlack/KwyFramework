using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record BlobFeatureInspectionRequest(
    IVisionImage Image,
    double MinimumGray,
    double MaximumGray,
    double MinimumArea,
    double MaximumArea,
    IVisionRegion? SearchRegion = null,
    int MaximumCount = int.MaxValue);

public sealed record VisionBlobFeature(
    double Area,
    VisionPoint Center,
    VisionRectangle Bounds,
    VisionRotatedRectangle OrientedBounds,
    double Circularity,
    double Roundness,
    double ContourLength,
    double MeanGray,
    double MinimumGray,
    double MaximumGray,
    double GrayRange);

public sealed record BlobFeatureInspectionResult(
    IReadOnlyList<VisionBlobFeature> Blobs);
