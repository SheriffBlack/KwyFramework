using HalconDotNet;
using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Halcon.Internal;

internal static class HalconRegionFactory
{
    public static HRegion? Create(IVisionRegion? region) => region switch
    {
        null => null,
        RectangleRegion rectangle => CreateRectangle(rectangle.Rectangle),
        RotatedRectangleRegion rectangle => CreateRotatedRectangle(rectangle.Rectangle),
        CircleRegion circle => new HRegion(
            circle.Circle.Center.Y,
            circle.Circle.Center.X,
            circle.Circle.Radius),
        EllipseRegion ellipse => CreateEllipse(ellipse.Ellipse),
        PolygonRegion polygon => CreatePolygon(polygon.Points),
        ContourRegion contour => CreatePolygon(contour.Contour.Points),
        CompositeRegion composite => CreateComposite(composite),
        _ => throw new NotSupportedException(
            $"HALCON search region {region.GetType().Name} is not supported yet.")
    };

    private static HRegion CreateRectangle(VisionRectangle rectangle)
        => new(
            rectangle.Y,
            rectangle.X,
            rectangle.Y + rectangle.Height - 1,
            rectangle.X + rectangle.Width - 1);

    private static HRegion CreateRotatedRectangle(VisionRotatedRectangle rectangle)
    {
        var region = new HRegion();
        region.GenRectangle2(
            rectangle.Center.Y,
            rectangle.Center.X,
            rectangle.AngleRadians,
            rectangle.Width / 2,
            rectangle.Height / 2);
        return region;
    }

    private static HRegion CreateEllipse(VisionEllipse ellipse)
    {
        var region = new HRegion();
        region.GenEllipse(
            ellipse.Center.Y,
            ellipse.Center.X,
            ellipse.AngleRadians,
            ellipse.RadiusX,
            ellipse.RadiusY);
        return region;
    }

    private static HRegion CreatePolygon(IReadOnlyList<VisionPoint> points)
    {
        var rows = new HTuple(points.Select(point => point.Y).ToArray());
        var columns = new HTuple(points.Select(point => point.X).ToArray());
        var region = new HRegion();
        region.GenRegionPolygonFilled(rows, columns);
        return region;
    }

    private static HRegion CreateComposite(CompositeRegion composite)
    {
        HRegion current = Create(composite.Outer)
            ?? throw new InvalidOperationException("A composite outer region cannot be empty.");
        try
        {
            foreach (IVisionRegion hole in composite.Holes)
            {
                using HRegion holeRegion = Create(hole)
                    ?? throw new InvalidOperationException("A composite hole region cannot be empty.");
                HRegion difference = current.Difference(holeRegion);
                current.Dispose();
                current = difference;
            }

            HRegion result = current;
            current = null!;
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }
}
