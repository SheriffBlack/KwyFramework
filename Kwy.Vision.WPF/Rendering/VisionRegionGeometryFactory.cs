using Kwy.Vision.Abstractions.Geometry;
using System.Windows;
using System.Windows.Media;

namespace Kwy.Vision.WPF.Rendering;

/// <summary>Converts backend-independent vision regions into WPF geometry in image coordinates.</summary>
public static class VisionRegionGeometryFactory
{
    public static Geometry Create(IVisionRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Geometry geometry = region switch
        {
            RectangleRegion rectangle => new RectangleGeometry(ToRect(rectangle.Rectangle)),
            RotatedRectangleRegion rectangle => CreateRotatedRectangle(rectangle.Rectangle),
            CircleRegion circle => new EllipseGeometry(ToPoint(circle.Circle.Center), circle.Circle.Radius, circle.Circle.Radius),
            EllipseRegion ellipse => CreateEllipse(ellipse.Ellipse),
            PolygonRegion polygon => CreatePolygon(polygon.Points),
            ContourRegion contour => CreatePolygon(contour.Contour.Points),
            CompositeRegion composite => CreateComposite(composite),
            _ => throw new NotSupportedException($"Unsupported vision region type: {region.GetType().FullName}.")
        };
        geometry.Freeze();
        return geometry;
    }

    private static Geometry CreateRotatedRectangle(VisionRotatedRectangle rectangle)
    {
        double halfWidth = rectangle.Width / 2;
        double halfHeight = rectangle.Height / 2;
        double cosine = Math.Cos(rectangle.AngleRadians);
        double sine = Math.Sin(rectangle.AngleRadians);
        VisionPoint[] local =
        [
            new(-halfWidth, -halfHeight),
            new(halfWidth, -halfHeight),
            new(halfWidth, halfHeight),
            new(-halfWidth, halfHeight)
        ];
        return CreatePolygon(local.Select(point => new VisionPoint(
            rectangle.Center.X + point.X * cosine - point.Y * sine,
            rectangle.Center.Y + point.X * sine + point.Y * cosine)).ToArray());
    }

    private static Geometry CreateEllipse(VisionEllipse ellipse)
    {
        var geometry = new EllipseGeometry(ToPoint(ellipse.Center), ellipse.RadiusX, ellipse.RadiusY)
        {
            Transform = new RotateTransform(
                ellipse.AngleRadians * 180 / Math.PI,
                ellipse.Center.X,
                ellipse.Center.Y)
        };
        return geometry;
    }

    private static Geometry CreatePolygon(IReadOnlyList<VisionPoint> points)
    {
        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(ToPoint(points[0]), true, true);
        context.PolyLineTo(points.Skip(1).Select(ToPoint).ToArray(), true, true);
        return geometry;
    }

    private static Geometry CreateComposite(CompositeRegion composite)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(Create(composite.Outer));
        foreach (IVisionRegion hole in composite.Holes)
        {
            group.Children.Add(Create(hole));
        }
        return group;
    }

    private static Point ToPoint(VisionPoint point) => new(point.X, point.Y);

    private static Rect ToRect(VisionRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
