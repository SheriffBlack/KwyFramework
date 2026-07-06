using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionRobustFittingMode
{
    Regression,
    Tukey,
    Huber,
    Drop,
    Gauss
}

public sealed record LineFittingRequest(
    IReadOnlyList<VisionPoint> Points,
    VisionRobustFittingMode Mode = VisionRobustFittingMode.Tukey,
    int ClippingEndPoints = 0,
    int Iterations = 5,
    double ClippingFactor = 2.0);

public sealed record LineFittingResult(
    VisionLine Line,
    VisionPoint StartPoint,
    VisionPoint EndPoint,
    VisionPoint Normal,
    double Distance,
    double MeanResidual,
    double MaxResidual);

public sealed record CircleFittingRequest(
    IReadOnlyList<VisionPoint> Points,
    VisionRobustFittingMode Mode = VisionRobustFittingMode.Tukey,
    int ClippingEndPoints = 0,
    int Iterations = 5,
    double ClippingFactor = 2.0);

public sealed record CircleFittingResult(
    VisionCircle Circle,
    double StartAngleRadians,
    double EndAngleRadians,
    string PointOrder,
    double MeanResidual,
    double MaxResidual);

public enum VisionContourFitShape
{
    Line,
    Circle,
    RotatedRectangle
}

public sealed record ContourFittingRequest(
    VisionContour Contour,
    VisionContourFitShape Shape,
    VisionRobustFittingMode Mode = VisionRobustFittingMode.Tukey,
    int ClippingEndPoints = 0,
    int Iterations = 5,
    double ClippingFactor = 2.0);

public sealed record ContourFittingResult(
    VisionContourFitShape Shape,
    VisionLine? Line = null,
    VisionCircle? Circle = null,
    VisionRotatedRectangle? RotatedRectangle = null,
    double MeanResidual = 0,
    double MaxResidual = 0);
