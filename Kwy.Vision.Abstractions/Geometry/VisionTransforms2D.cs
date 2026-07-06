namespace Kwy.Vision.Abstractions.Geometry;

public readonly record struct VisionPose2D(double X, double Y, double AngleRadians);

public readonly record struct VisionTransform2D(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public static VisionTransform2D Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public VisionPoint Transform(VisionPoint point)
        => new(
            M11 * point.X + M12 * point.Y + OffsetX,
            M21 * point.X + M22 * point.Y + OffsetY);
}
