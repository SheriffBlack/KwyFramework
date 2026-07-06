using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record VisionPointCorrespondence(
    VisionPoint ImagePoint,
    VisionPoint WorldPoint);

public sealed record PlanarCalibrationRequest(
    IReadOnlyList<VisionPointCorrespondence> Correspondences);

public sealed record VisionCalibrationResidual(
    VisionPoint ImagePoint,
    VisionPoint ExpectedWorldPoint,
    VisionPoint ActualWorldPoint,
    double Error);

public sealed record PlanarCalibrationResult(
    VisionTransform2D ImageToWorld,
    VisionTransform2D WorldToImage,
    double RootMeanSquareError,
    double MaximumError,
    IReadOnlyList<VisionCalibrationResidual> Residuals);

public sealed record RotationCenterCalibrationRequest(
    IReadOnlyList<VisionPoint> RotationPoints,
    VisionTransform2D? ImageToWorld = null);

public sealed record RotationCenterCalibrationResult(
    VisionPoint PixelCenter,
    double RadiusPixels,
    double ResidualPixels,
    VisionPoint? WorldCenter = null);
