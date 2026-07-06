using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record BlobInspectionRequest(
    IVisionImage Image,
    double MinimumGray,
    double MaximumGray,
    double MinimumArea,
    double MaximumArea,
    IVisionRegion? SearchRegion = null,
    int MaximumCount = int.MaxValue);

public sealed record VisionBlob(
    double Area,
    VisionPoint Center,
    VisionRectangle Bounds);

public sealed record BlobInspectionResult(IReadOnlyList<VisionBlob> Blobs);
