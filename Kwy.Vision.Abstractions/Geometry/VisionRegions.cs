namespace Kwy.Vision.Abstractions.Geometry;

public interface IVisionRegion
{
}

public sealed record RectangleRegion(VisionRectangle Rectangle) : IVisionRegion;

public sealed record RotatedRectangleRegion(VisionRotatedRectangle Rectangle) : IVisionRegion;

public sealed record CircleRegion(VisionCircle Circle) : IVisionRegion;

public sealed record EllipseRegion(VisionEllipse Ellipse) : IVisionRegion;

public sealed record PolygonRegion : IVisionRegion
{
    public PolygonRegion(IEnumerable<VisionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points.ToArray();
        if (Points.Count < 3)
        {
            throw new ArgumentException("A polygon region requires at least three points.", nameof(points));
        }
    }

    public IReadOnlyList<VisionPoint> Points { get; }
}

public sealed record ContourRegion : IVisionRegion
{
    public ContourRegion(VisionContour contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        if (!contour.IsClosed)
        {
            throw new ArgumentException("A contour region requires a closed contour.", nameof(contour));
        }

        Contour = contour;
    }

    public VisionContour Contour { get; }
}

/// <summary>An outer region with one or more regions removed as holes.</summary>
public sealed record CompositeRegion : IVisionRegion
{
    public CompositeRegion(IVisionRegion outer, IEnumerable<IVisionRegion> holes)
    {
        Outer = outer ?? throw new ArgumentNullException(nameof(outer));
        ArgumentNullException.ThrowIfNull(holes);
        Holes = holes.ToArray();
        if (Holes.Count == 0)
        {
            throw new ArgumentException("A composite region requires at least one hole.", nameof(holes));
        }

        if (Holes.Any(item => item == null))
        {
            throw new ArgumentException("A composite region cannot contain a null hole.", nameof(holes));
        }
    }

    public IVisionRegion Outer { get; }

    public IReadOnlyList<IVisionRegion> Holes { get; }
}
