using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Halcon.Algorithms;

internal static class HalconFittingUtilities
{
    public static HXLDCont CreateContour(IReadOnlyList<VisionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var rows = new HTuple(points.Select(point => point.Y).ToArray());
        var columns = new HTuple(points.Select(point => point.X).ToArray());
        var contour = new HXLDCont();
        contour.GenContourPolygonXld(rows, columns);
        return contour;
    }

    public static string ToHalconAlgorithm(VisionRobustFittingMode mode) => mode switch
    {
        VisionRobustFittingMode.Regression => "regression",
        VisionRobustFittingMode.Tukey => "tukey",
        VisionRobustFittingMode.Huber => "huber",
        VisionRobustFittingMode.Drop => "drop",
        VisionRobustFittingMode.Gauss => "gauss",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static (double Mean, double Max) CalculateLineResiduals(
        IReadOnlyList<VisionPoint> points,
        double normalRow,
        double normalColumn,
        double distance)
    {
        double sum = 0;
        double max = 0;
        foreach (VisionPoint point in points)
        {
            double residual = Math.Abs(normalRow * point.Y + normalColumn * point.X + distance);
            sum += residual;
            max = Math.Max(max, residual);
        }

        return (sum / points.Count, max);
    }

    public static (double Mean, double Max) CalculateCircleResiduals(
        IReadOnlyList<VisionPoint> points,
        VisionCircle circle)
    {
        double sum = 0;
        double max = 0;
        foreach (VisionPoint point in points)
        {
            double dx = point.X - circle.Center.X;
            double dy = point.Y - circle.Center.Y;
            double residual = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - circle.Radius);
            sum += residual;
            max = Math.Max(max, residual);
        }

        return (sum / points.Count, max);
    }

    public static void ValidateFitting(
        IReadOnlyList<VisionPoint> points,
        int minimumPointCount,
        int clippingEndPoints,
        int iterations,
        double clippingFactor)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < minimumPointCount)
        {
            throw new ArgumentException($"At least {minimumPointCount} points are required.", nameof(points));
        }

        if (points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            throw new ArgumentException("Fitting points must be finite.", nameof(points));
        }

        if (clippingEndPoints < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clippingEndPoints));
        }

        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        if (!double.IsFinite(clippingFactor) || clippingFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clippingFactor));
        }
    }
}
