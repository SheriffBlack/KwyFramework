using System.Windows;

namespace Kwy.Vision.WPF.Interaction;

public enum RectangleRoiHandle
{
    None,
    Body,
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomRight,
    BottomLeft
}

/// <summary>
/// Backend-independent rectangle ROI geometry, hit testing, movement, and resizing.
/// Coordinates passed to editing methods are image coordinates; hit testing uses view coordinates.
/// </summary>
public sealed class RectangleRoiEditor
{
    public double HandleSize { get; set; } = 8;

    public double MinimumSize { get; set; } = 1;

    public RectangleRoiHandle HitTest(Rect viewRectangle, Point viewPoint)
    {
        if (viewRectangle.Width <= 0 || viewRectangle.Height <= 0)
        {
            return RectangleRoiHandle.None;
        }

        Rect expanded = viewRectangle;
        expanded.Inflate(HandleSize, HandleSize);
        if (!expanded.Contains(viewPoint))
        {
            return RectangleRoiHandle.None;
        }

        foreach ((RectangleRoiHandle handle, Rect bounds) in GetHandleRects(viewRectangle))
        {
            if (bounds.Contains(viewPoint))
            {
                return handle;
            }
        }

        if (viewRectangle.Contains(viewPoint))
        {
            return RectangleRoiHandle.Body;
        }

        if (Math.Abs(viewPoint.X - viewRectangle.Left) <= HandleSize)
        {
            return RectangleRoiHandle.Left;
        }

        if (Math.Abs(viewPoint.X - viewRectangle.Right) <= HandleSize)
        {
            return RectangleRoiHandle.Right;
        }

        if (Math.Abs(viewPoint.Y - viewRectangle.Top) <= HandleSize)
        {
            return RectangleRoiHandle.Top;
        }

        if (Math.Abs(viewPoint.Y - viewRectangle.Bottom) <= HandleSize)
        {
            return RectangleRoiHandle.Bottom;
        }

        return RectangleRoiHandle.None;
    }

    public Rect Create(Point start, Point current, Size imageSize)
        => NormalizeAndClamp(new Rect(start, current), imageSize);

    public Rect Move(Rect original, Vector delta, Size imageSize)
    {
        Rect normalized = Normalize(original);
        double maxX = Math.Max(0, imageSize.Width - normalized.Width);
        double maxY = Math.Max(0, imageSize.Height - normalized.Height);
        return new Rect(
            Math.Clamp(normalized.X + delta.X, 0, maxX),
            Math.Clamp(normalized.Y + delta.Y, 0, maxY),
            Math.Min(normalized.Width, imageSize.Width),
            Math.Min(normalized.Height, imageSize.Height));
    }

    public Rect Resize(Rect original, RectangleRoiHandle handle, Point current, Size imageSize)
    {
        Rect normalized = Normalize(original);
        double left = normalized.Left;
        double top = normalized.Top;
        double right = normalized.Right;
        double bottom = normalized.Bottom;

        if (handle is RectangleRoiHandle.Left or RectangleRoiHandle.TopLeft or RectangleRoiHandle.BottomLeft)
        {
            left = current.X;
        }
        if (handle is RectangleRoiHandle.Right or RectangleRoiHandle.TopRight or RectangleRoiHandle.BottomRight)
        {
            right = current.X;
        }
        if (handle is RectangleRoiHandle.Top or RectangleRoiHandle.TopLeft or RectangleRoiHandle.TopRight)
        {
            top = current.Y;
        }
        if (handle is RectangleRoiHandle.Bottom or RectangleRoiHandle.BottomLeft or RectangleRoiHandle.BottomRight)
        {
            bottom = current.Y;
        }

        return NormalizeAndClamp(new Rect(new Point(left, top), new Point(right, bottom)), imageSize);
    }

    public Rect NormalizeAndClamp(Rect rectangle, Size imageSize)
    {
        Rect normalized = Normalize(rectangle);
        double width = Math.Max(0, imageSize.Width);
        double height = Math.Max(0, imageSize.Height);
        double minimumWidth = Math.Min(MinimumSize, width);
        double minimumHeight = Math.Min(MinimumSize, height);
        double left = Math.Clamp(normalized.Left, 0, Math.Max(0, width - minimumWidth));
        double top = Math.Clamp(normalized.Top, 0, Math.Max(0, height - minimumHeight));
        double right = Math.Clamp(normalized.Right, Math.Min(width, left + minimumWidth), width);
        double bottom = Math.Clamp(normalized.Bottom, Math.Min(height, top + minimumHeight), height);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public IReadOnlyList<(RectangleRoiHandle Handle, Rect Bounds)> GetHandleRects(Rect viewRectangle)
    {
        Point[] points =
        [
            new(viewRectangle.Left, viewRectangle.Top),
            new(viewRectangle.Left + viewRectangle.Width / 2, viewRectangle.Top),
            new(viewRectangle.Right, viewRectangle.Top),
            new(viewRectangle.Right, viewRectangle.Top + viewRectangle.Height / 2),
            new(viewRectangle.Right, viewRectangle.Bottom),
            new(viewRectangle.Left + viewRectangle.Width / 2, viewRectangle.Bottom),
            new(viewRectangle.Left, viewRectangle.Bottom),
            new(viewRectangle.Left, viewRectangle.Top + viewRectangle.Height / 2)
        ];
        RectangleRoiHandle[] handles =
        [
            RectangleRoiHandle.TopLeft,
            RectangleRoiHandle.Top,
            RectangleRoiHandle.TopRight,
            RectangleRoiHandle.Right,
            RectangleRoiHandle.BottomRight,
            RectangleRoiHandle.Bottom,
            RectangleRoiHandle.BottomLeft,
            RectangleRoiHandle.Left
        ];

        var result = new (RectangleRoiHandle, Rect)[points.Length];
        for (int index = 0; index < points.Length; index++)
        {
            result[index] = (handles[index], CreateHandle(points[index]));
        }

        return result;
    }

    private Rect CreateHandle(Point center)
        => new(center.X - HandleSize / 2, center.Y - HandleSize / 2, HandleSize, HandleSize);

    private static Rect Normalize(Rect rectangle)
        => new(
            Math.Min(rectangle.Left, rectangle.Right),
            Math.Min(rectangle.Top, rectangle.Bottom),
            Math.Abs(rectangle.Width),
            Math.Abs(rectangle.Height));
}
