namespace Kwy.Vision.Abstractions.Geometry;

public readonly record struct VisionRectangle(double X, double Y, double Width, double Height);

public readonly record struct VisionRotatedRectangle(
    VisionPoint Center,
    double Width,
    double Height,
    double AngleRadians);

public readonly record struct VisionCircle(VisionPoint Center, double Radius);

/// <summary>An ellipse whose rotation is expressed in radians.</summary>
public readonly record struct VisionEllipse(
    VisionPoint Center,
    double RadiusX,
    double RadiusY,
    double AngleRadians);

/// <summary>A circular arc. Positive sweep follows the image coordinate convention.</summary>
public readonly record struct VisionArc(
    VisionPoint Center,
    double Radius,
    double StartAngleRadians,
    double SweepAngleRadians);

/// <summary>An ordered, open sequence of connected line segments.</summary>
public sealed record VisionPolyline
{
    public VisionPolyline(IEnumerable<VisionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points.ToArray();
        if (Points.Count < 2)
        {
            throw new ArgumentException("A polyline requires at least two points.", nameof(points));
        }
    }

    public IReadOnlyList<VisionPoint> Points { get; }
}

/// <summary>An arbitrary sampled contour that may be open or closed.</summary>
public sealed record VisionContour
{
    public VisionContour(IEnumerable<VisionPoint> points, bool isClosed)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = points.ToArray();
        int minimumCount = isClosed ? 3 : 2;
        if (Points.Count < minimumCount)
        {
            throw new ArgumentException(
                $"A {(isClosed ? "closed" : "open")} contour requires at least {minimumCount} points.",
                nameof(points));
        }

        IsClosed = isClosed;
    }

    public IReadOnlyList<VisionPoint> Points { get; }

    public bool IsClosed { get; }
}

/// <summary>A collection of independent line segments.</summary>
public sealed record VisionLineSet
{
    public VisionLineSet(IEnumerable<VisionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines = lines.ToArray();
        if (Lines.Count == 0)
        {
            throw new ArgumentException("A line set requires at least one line.", nameof(lines));
        }
    }

    public IReadOnlyList<VisionLine> Lines { get; }
}
