using Kwy.Vision.Abstractions.Geometry;
using System.Windows;

namespace Kwy.Vision.WPF.Interaction;

public enum VisionRoiCreationKind
{
    Rectangle,
    Circle,
    RotatedRectangle
}

public enum VisionRoiHandle
{
    None,
    Body,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
    Radius,
    Rotation
}

public sealed class VisionRegionEditor
{
    private readonly RectangleRoiEditor rectangleEditor = new();

    public double HandleSize
    {
        get => rectangleEditor.HandleSize;
        set => rectangleEditor.HandleSize = value;
    }

    public double MinimumSize
    {
        get => rectangleEditor.MinimumSize;
        set => rectangleEditor.MinimumSize = value;
    }

    public double RotationHandleDistance { get; set; } = 24;

    public IVisionRegion Create(VisionRoiCreationKind kind, Point start, Point current, Size imageSize)
        => kind switch
        {
            VisionRoiCreationKind.Circle => CreateCircle(start, current, imageSize),
            VisionRoiCreationKind.RotatedRectangle => ToRotatedRectangle(rectangleEditor.Create(start, current, imageSize)),
            _ => ToRectangleRegion(rectangleEditor.Create(start, current, imageSize))
        };

    public IVisionRegion Move(IVisionRegion region, Vector delta, Size imageSize)
        => region switch
        {
            RectangleRegion rectangle => ToRectangleRegion(rectangleEditor.Move(ToRect(rectangle.Rectangle), delta, imageSize)),
            CircleRegion circle => MoveCircle(circle, delta, imageSize),
            RotatedRectangleRegion rectangle => MoveRotatedRectangle(rectangle, delta, imageSize),
            _ => region
        };

    public IVisionRegion Resize(
        IVisionRegion region,
        VisionRoiHandle handle,
        Point currentImagePoint,
        Size imageSize)
        => region switch
        {
            RectangleRegion rectangle => ResizeRectangle(rectangle, handle, currentImagePoint, imageSize),
            CircleRegion circle when handle == VisionRoiHandle.Radius => ResizeCircle(circle, currentImagePoint, imageSize),
            RotatedRectangleRegion rectangle when handle == VisionRoiHandle.Rotation => RotateRectangle(rectangle, currentImagePoint),
            RotatedRectangleRegion rectangle => ResizeRotatedRectangle(rectangle, currentImagePoint, imageSize),
            _ => region
        };

    public VisionRoiHandle HitTest(
        IVisionRegion region,
        Point viewPoint,
        Func<Point, Point> imageToView,
        Func<Point, Point> viewToImage,
        double viewScale)
    {
        foreach ((VisionRoiHandle handle, Point point) in GetHandlePoints(region, imageToView, viewScale))
        {
            if (CreateHandleRect(point).Contains(viewPoint))
            {
                return handle;
            }
        }

        Point imagePoint = viewToImage(viewPoint);
        return Contains(region, imagePoint) ? VisionRoiHandle.Body : VisionRoiHandle.None;
    }

    public IReadOnlyList<(VisionRoiHandle Handle, Point ViewPoint)> GetHandlePoints(
        IVisionRegion region,
        Func<Point, Point> imageToView,
        double viewScale)
    {
        return region switch
        {
            RectangleRegion rectangle => GetRectangleHandles(rectangle, imageToView),
            CircleRegion circle =>
            [
                (VisionRoiHandle.Radius, imageToView(new Point(
                    circle.Circle.Center.X + circle.Circle.Radius,
                    circle.Circle.Center.Y)))
            ],
            RotatedRectangleRegion rectangle => GetRotatedRectangleHandles(rectangle, imageToView, viewScale),
            _ => Array.Empty<(VisionRoiHandle, Point)>()
        };
    }

    public Rect GetHandleRect(Point center) => CreateHandleRect(center);

    private IReadOnlyList<(VisionRoiHandle, Point)> GetRectangleHandles(
        RectangleRegion region,
        Func<Point, Point> imageToView)
    {
        VisionRectangle rectangle = region.Rectangle;
        return
        [
            (VisionRoiHandle.TopLeft, imageToView(new Point(rectangle.X, rectangle.Y))),
            (VisionRoiHandle.Top, imageToView(new Point(rectangle.X + rectangle.Width / 2, rectangle.Y))),
            (VisionRoiHandle.TopRight, imageToView(new Point(rectangle.X + rectangle.Width, rectangle.Y))),
            (VisionRoiHandle.Right, imageToView(new Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height / 2))),
            (VisionRoiHandle.BottomRight, imageToView(new Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height))),
            (VisionRoiHandle.Bottom, imageToView(new Point(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height))),
            (VisionRoiHandle.BottomLeft, imageToView(new Point(rectangle.X, rectangle.Y + rectangle.Height))),
            (VisionRoiHandle.Left, imageToView(new Point(rectangle.X, rectangle.Y + rectangle.Height / 2)))
        ];
    }

    private IReadOnlyList<(VisionRoiHandle, Point)> GetRotatedRectangleHandles(
        RotatedRectangleRegion region,
        Func<Point, Point> imageToView,
        double viewScale)
    {
        VisionRotatedRectangle rectangle = region.Rectangle;
        Point[] corners = GetRotatedCorners(rectangle);
        double distance = RotationHandleDistance / Math.Max(viewScale, 0.0001);
        Point rotation = TransformLocal(rectangle, new Point(0, -rectangle.Height / 2 - distance));
        return
        [
            (VisionRoiHandle.TopLeft, imageToView(corners[0])),
            (VisionRoiHandle.TopRight, imageToView(corners[1])),
            (VisionRoiHandle.BottomRight, imageToView(corners[2])),
            (VisionRoiHandle.BottomLeft, imageToView(corners[3])),
            (VisionRoiHandle.Rotation, imageToView(rotation))
        ];
    }

    private IVisionRegion ResizeRectangle(
        RectangleRegion region,
        VisionRoiHandle handle,
        Point current,
        Size imageSize)
    {
        RectangleRoiHandle rectangleHandle = handle switch
        {
            VisionRoiHandle.TopLeft => RectangleRoiHandle.TopLeft,
            VisionRoiHandle.Top => RectangleRoiHandle.Top,
            VisionRoiHandle.TopRight => RectangleRoiHandle.TopRight,
            VisionRoiHandle.Right => RectangleRoiHandle.Right,
            VisionRoiHandle.BottomRight => RectangleRoiHandle.BottomRight,
            VisionRoiHandle.Bottom => RectangleRoiHandle.Bottom,
            VisionRoiHandle.BottomLeft => RectangleRoiHandle.BottomLeft,
            VisionRoiHandle.Left => RectangleRoiHandle.Left,
            _ => RectangleRoiHandle.None
        };
        return ToRectangleRegion(rectangleEditor.Resize(ToRect(region.Rectangle), rectangleHandle, current, imageSize));
    }

    private static CircleRegion CreateCircle(Point center, Point current, Size imageSize)
    {
        double radius = Distance(center, current);
        double maximum = Math.Max(0, Math.Min(
            Math.Min(center.X, imageSize.Width - center.X),
            Math.Min(center.Y, imageSize.Height - center.Y)));
        return new CircleRegion(new VisionCircle(ToVisionPoint(center), Math.Min(radius, maximum)));
    }

    private static CircleRegion MoveCircle(CircleRegion region, Vector delta, Size imageSize)
    {
        double radius = region.Circle.Radius;
        return new CircleRegion(new VisionCircle(
            new VisionPoint(
                Math.Clamp(region.Circle.Center.X + delta.X, radius, Math.Max(radius, imageSize.Width - radius)),
                Math.Clamp(region.Circle.Center.Y + delta.Y, radius, Math.Max(radius, imageSize.Height - radius))),
            radius));
    }

    private CircleRegion ResizeCircle(CircleRegion region, Point current, Size imageSize)
        => CreateCircle(ToPoint(region.Circle.Center), current, imageSize);

    private static RotatedRectangleRegion MoveRotatedRectangle(
        RotatedRectangleRegion region,
        Vector delta,
        Size imageSize)
    {
        VisionRotatedRectangle rectangle = region.Rectangle;
        (double extentX, double extentY) = GetRotatedExtents(rectangle);
        return new RotatedRectangleRegion(rectangle with
        {
            Center = new VisionPoint(
                Math.Clamp(rectangle.Center.X + delta.X, extentX, Math.Max(extentX, imageSize.Width - extentX)),
                Math.Clamp(rectangle.Center.Y + delta.Y, extentY, Math.Max(extentY, imageSize.Height - extentY)))
        });
    }

    private RotatedRectangleRegion ResizeRotatedRectangle(
        RotatedRectangleRegion region,
        Point current,
        Size imageSize)
    {
        Point clamped = new(
            Math.Clamp(current.X, 0, imageSize.Width),
            Math.Clamp(current.Y, 0, imageSize.Height));
        Point local = InverseTransform(region.Rectangle, clamped);
        return new RotatedRectangleRegion(region.Rectangle with
        {
            Width = Math.Max(MinimumSize, Math.Abs(local.X) * 2),
            Height = Math.Max(MinimumSize, Math.Abs(local.Y) * 2)
        });
    }

    private static RotatedRectangleRegion RotateRectangle(RotatedRectangleRegion region, Point current)
    {
        VisionPoint center = region.Rectangle.Center;
        double angle = Math.Atan2(current.Y - center.Y, current.X - center.X) + Math.PI / 2;
        return new RotatedRectangleRegion(region.Rectangle with { AngleRadians = angle });
    }

    private static bool Contains(IVisionRegion region, Point point)
        => region switch
        {
            RectangleRegion rectangle => ToRect(rectangle.Rectangle).Contains(point),
            CircleRegion circle => Distance(ToPoint(circle.Circle.Center), point) <= circle.Circle.Radius,
            RotatedRectangleRegion rectangle => ContainsRotatedRectangle(rectangle.Rectangle, point),
            _ => false
        };

    private static bool ContainsRotatedRectangle(VisionRotatedRectangle rectangle, Point point)
    {
        Point local = InverseTransform(rectangle, point);
        return Math.Abs(local.X) <= rectangle.Width / 2 && Math.Abs(local.Y) <= rectangle.Height / 2;
    }

    private static Point[] GetRotatedCorners(VisionRotatedRectangle rectangle)
        =>
        [
            TransformLocal(rectangle, new Point(-rectangle.Width / 2, -rectangle.Height / 2)),
            TransformLocal(rectangle, new Point(rectangle.Width / 2, -rectangle.Height / 2)),
            TransformLocal(rectangle, new Point(rectangle.Width / 2, rectangle.Height / 2)),
            TransformLocal(rectangle, new Point(-rectangle.Width / 2, rectangle.Height / 2))
        ];

    private static Point TransformLocal(VisionRotatedRectangle rectangle, Point point)
    {
        double cosine = Math.Cos(rectangle.AngleRadians);
        double sine = Math.Sin(rectangle.AngleRadians);
        return new Point(
            rectangle.Center.X + point.X * cosine - point.Y * sine,
            rectangle.Center.Y + point.X * sine + point.Y * cosine);
    }

    private static Point InverseTransform(VisionRotatedRectangle rectangle, Point point)
    {
        double x = point.X - rectangle.Center.X;
        double y = point.Y - rectangle.Center.Y;
        double cosine = Math.Cos(rectangle.AngleRadians);
        double sine = Math.Sin(rectangle.AngleRadians);
        return new Point(x * cosine + y * sine, -x * sine + y * cosine);
    }

    private static (double X, double Y) GetRotatedExtents(VisionRotatedRectangle rectangle)
    {
        double cosine = Math.Abs(Math.Cos(rectangle.AngleRadians));
        double sine = Math.Abs(Math.Sin(rectangle.AngleRadians));
        return ((rectangle.Width * cosine + rectangle.Height * sine) / 2,
            (rectangle.Width * sine + rectangle.Height * cosine) / 2);
    }

    private Rect CreateHandleRect(Point center)
        => new(center.X - HandleSize / 2, center.Y - HandleSize / 2, HandleSize, HandleSize);

    private static RotatedRectangleRegion ToRotatedRectangle(Rect rectangle)
        => new(new VisionRotatedRectangle(
            new VisionPoint(rectangle.X + rectangle.Width / 2, rectangle.Y + rectangle.Height / 2),
            rectangle.Width,
            rectangle.Height,
            0));

    private static RectangleRegion ToRectangleRegion(Rect rectangle)
        => new(new VisionRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height));

    private static Rect ToRect(VisionRectangle rectangle)
        => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private static Point ToPoint(VisionPoint point) => new(point.X, point.Y);

    private static VisionPoint ToVisionPoint(Point point) => new(point.X, point.Y);

    private static double Distance(Point first, Point second)
        => Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
}
