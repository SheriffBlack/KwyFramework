using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;
using KwyTemplate.Vision.ViewModels.Items;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KwyTemplate.Vision.Controls;

public sealed class VisionImageViewer : FrameworkElement
{
    public static readonly DependencyProperty ImageItemProperty =
        DependencyProperty.Register(
            nameof(ImageItem),
            typeof(VisionImagePanelItemViewModel),
            typeof(VisionImageViewer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnImageItemChanged));

    public static readonly DependencyProperty RoiProperty =
        DependencyProperty.Register(
            nameof(Roi),
            typeof(Rect?),
            typeof(VisionImageViewer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    private const double MinimumScale = 0.02;
    private const double MaximumScale = 80;
    private const double HandleSize = 8;
    private const double MinimumRoiSize = 1;
    private const string HelpText = "滚轮缩放，右键/中键平移，左键新建/编辑 ROI，Delete 删除 ROI，双击适配窗口";

    private double scale = 1;
    private Vector offset;
    private bool viewInitialized;
    private InteractionMode interactionMode;
    private RoiHitType activeHitType = RoiHitType.None;
    private Point dragStartView;
    private Point lastMouseView;
    private Point roiStartImage;
    private Rect dragStartRoi;
    private string statusText = HelpText;
    private WriteableBitmap? renderBitmap;
    private VisionImagePanelItemViewModel? renderBitmapSource;
    private CancellationTokenSource? imageLoadCts;

    public VisionImageViewer()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
    }

    public VisionImagePanelItemViewModel? ImageItem
    {
        get => (VisionImagePanelItemViewModel?)GetValue(ImageItemProperty);
        set => SetValue(ImageItemProperty, value);
    }

    public Rect? Roi
    {
        get => (Rect?)GetValue(RoiProperty);
        set => SetValue(RoiProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        DrawBackground(dc);

        VisionImagePanelItemViewModel? item = ImageItem;
        if (item == null)
        {
            DrawEmpty(dc);
            return;
        }

        if (!item.HasPixels || !EnsureRenderBitmap(item))
        {
            DrawLoading(dc, item);
            return;
        }

        EnsureViewInitialized(item);
        dc.DrawImage(renderBitmap, GetImageViewRect(item));
        DrawOverlays(dc, item);
        DrawRoi(dc);
        DrawImageInfo(dc, item);
        DrawStatus(dc);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (ImageItem == null || renderBitmap == null)
        {
            return;
        }

        Focus();
        Point mouse = e.GetPosition(this);
        Point imageBefore = ViewToImage(mouse);
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        scale = Math.Clamp(scale * factor, MinimumScale, MaximumScale);
        Point viewAfter = ImageToView(imageBefore);
        offset += mouse - viewAfter;
        UpdatePixelStatus(mouse);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        CaptureMouse();
        dragStartView = e.GetPosition(this);
        lastMouseView = dragStartView;

        if (e.ClickCount >= 2)
        {
            FitToView();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton is MouseButton.Right or MouseButton.Middle)
        {
            interactionMode = InteractionMode.Pan;
            Cursor = Cursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        activeHitType = HitTestRoi(dragStartView);
        if (activeHitType == RoiHitType.Body && Roi is Rect bodyRoi)
        {
            interactionMode = InteractionMode.MoveRoi;
            dragStartRoi = bodyRoi;
        }
        else if (activeHitType != RoiHitType.None && Roi is Rect resizeRoi)
        {
            interactionMode = InteractionMode.ResizeRoi;
            dragStartRoi = resizeRoi;
        }
        else
        {
            interactionMode = InteractionMode.CreateRoi;
            roiStartImage = ClampImagePoint(ViewToImage(dragStartView));
            Roi = new Rect(roiStartImage, roiStartImage);
        }

        UpdateCursor(activeHitType);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point current = e.GetPosition(this);

        switch (interactionMode)
        {
            case InteractionMode.Pan:
                offset += current - lastMouseView;
                lastMouseView = current;
                InvalidateVisual();
                break;

            case InteractionMode.CreateRoi:
                Roi = NormalizeAndClamp(new Rect(roiStartImage, ClampImagePoint(ViewToImage(current))));
                UpdateRoiStatus();
                InvalidateVisual();
                break;

            case InteractionMode.MoveRoi:
                MoveRoi(current);
                UpdateRoiStatus();
                InvalidateVisual();
                break;

            case InteractionMode.ResizeRoi:
                ResizeRoi(current);
                UpdateRoiStatus();
                InvalidateVisual();
                break;

            default:
                activeHitType = HitTestRoi(current);
                UpdateCursor(activeHitType);
                UpdatePixelStatus(current);
                break;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (interactionMode is InteractionMode.CreateRoi or InteractionMode.MoveRoi or InteractionMode.ResizeRoi)
        {
            Roi = NormalizeAndClamp(Roi);
            UpdateRoiStatus();
        }

        interactionMode = InteractionMode.None;
        activeHitType = RoiHitType.None;
        Cursor = Cursors.Cross;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (interactionMode == InteractionMode.None)
        {
            statusText = HelpText;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Delete or Key.Back)
        {
            Roi = null;
            statusText = "ROI 已删除";
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        viewInitialized = false;
        InvalidateVisual();
    }

    private static void OnImageItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (VisionImageViewer)d;
        viewer.imageLoadCts?.Cancel();
        viewer.imageLoadCts?.Dispose();
        viewer.imageLoadCts = null;
        viewer.viewInitialized = false;
        viewer.renderBitmapSource = null;
        viewer.statusText = HelpText;
        if (e.NewValue is VisionImagePanelItemViewModel item)
        {
            viewer.BeginLoadImagePixels(item);
        }

        viewer.InvalidateVisual();
    }

    private async void BeginLoadImagePixels(VisionImagePanelItemViewModel item)
    {
        imageLoadCts = new CancellationTokenSource();
        CancellationToken token = imageLoadCts.Token;
        try
        {
            await item.EnsurePixelsAsync(token).ConfigureAwait(true);
            if (!token.IsCancellationRequested && ReferenceEquals(ImageItem, item))
            {
                renderBitmapSource = null;
                InvalidateVisual();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void MoveRoi(Point currentView)
    {
        Point currentImage = ViewToImage(currentView);
        Point startImage = ViewToImage(dragStartView);
        Vector delta = currentImage - startImage;
        Roi = NormalizeAndClamp(new Rect(
            dragStartRoi.X + delta.X,
            dragStartRoi.Y + delta.Y,
            dragStartRoi.Width,
            dragStartRoi.Height));
    }

    private void ResizeRoi(Point currentView)
    {
        Point currentImage = ClampImagePoint(ViewToImage(currentView));
        double left = dragStartRoi.Left;
        double top = dragStartRoi.Top;
        double right = dragStartRoi.Right;
        double bottom = dragStartRoi.Bottom;

        if (activeHitType is RoiHitType.Left or RoiHitType.TopLeft or RoiHitType.BottomLeft)
        {
            left = currentImage.X;
        }
        if (activeHitType is RoiHitType.Right or RoiHitType.TopRight or RoiHitType.BottomRight)
        {
            right = currentImage.X;
        }
        if (activeHitType is RoiHitType.Top or RoiHitType.TopLeft or RoiHitType.TopRight)
        {
            top = currentImage.Y;
        }
        if (activeHitType is RoiHitType.Bottom or RoiHitType.BottomLeft or RoiHitType.BottomRight)
        {
            bottom = currentImage.Y;
        }

        Roi = NormalizeAndClamp(new Rect(new Point(left, top), new Point(right, bottom)));
    }

    private RoiHitType HitTestRoi(Point viewPoint)
    {
        if (Roi is not Rect roi || roi.Width <= 0 || roi.Height <= 0)
        {
            return RoiHitType.None;
        }

        Rect viewRect = ImageRectToView(new VisionRectangle(roi.X, roi.Y, roi.Width, roi.Height));
        Rect expanded = viewRect;
        expanded.Inflate(HandleSize, HandleSize);
        if (!expanded.Contains(viewPoint))
        {
            return RoiHitType.None;
        }

        foreach ((RoiHitType type, Rect handle) in GetHandleRects(viewRect))
        {
            if (handle.Contains(viewPoint))
            {
                return type;
            }
        }

        if (viewRect.Contains(viewPoint))
        {
            return RoiHitType.Body;
        }

        double tolerance = HandleSize;
        if (Math.Abs(viewPoint.X - viewRect.Left) <= tolerance && viewPoint.Y >= viewRect.Top && viewPoint.Y <= viewRect.Bottom)
        {
            return RoiHitType.Left;
        }
        if (Math.Abs(viewPoint.X - viewRect.Right) <= tolerance && viewPoint.Y >= viewRect.Top && viewPoint.Y <= viewRect.Bottom)
        {
            return RoiHitType.Right;
        }
        if (Math.Abs(viewPoint.Y - viewRect.Top) <= tolerance && viewPoint.X >= viewRect.Left && viewPoint.X <= viewRect.Right)
        {
            return RoiHitType.Top;
        }
        if (Math.Abs(viewPoint.Y - viewRect.Bottom) <= tolerance && viewPoint.X >= viewRect.Left && viewPoint.X <= viewRect.Right)
        {
            return RoiHitType.Bottom;
        }

        return RoiHitType.None;
    }

    private void UpdateCursor(RoiHitType hitType)
    {
        Cursor = hitType switch
        {
            RoiHitType.Body => Cursors.SizeAll,
            RoiHitType.Left or RoiHitType.Right => Cursors.SizeWE,
            RoiHitType.Top or RoiHitType.Bottom => Cursors.SizeNS,
            RoiHitType.TopLeft or RoiHitType.BottomRight => Cursors.SizeNWSE,
            RoiHitType.TopRight or RoiHitType.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Cross
        };
    }

    private bool EnsureRenderBitmap(VisionImagePanelItemViewModel item)
    {
        byte[]? pixels = item.Pixels;
        if (item.Width <= 0 || item.Height <= 0 || item.Stride <= 0 || pixels is not { Length: > 0 })
        {
            renderBitmap = null;
            renderBitmapSource = null;
            return false;
        }

        PixelFormat pixelFormat;
        try
        {
            pixelFormat = ToWpfPixelFormat(item.PixelFormat);
        }
        catch (NotSupportedException)
        {
            renderBitmap = null;
            renderBitmapSource = null;
            return false;
        }

        if (!ReferenceEquals(renderBitmapSource, item))
        {
            if (renderBitmap == null ||
                renderBitmap.PixelWidth != item.Width ||
                renderBitmap.PixelHeight != item.Height ||
                renderBitmap.Format != pixelFormat)
            {
                renderBitmap = new WriteableBitmap(
                    item.Width,
                    item.Height,
                    96,
                    96,
                    pixelFormat,
                    null);
            }

            int requiredLength = checked(item.Stride * item.Height);
            if (pixels.Length < requiredLength)
            {
                renderBitmap = null;
                renderBitmapSource = null;
                return false;
            }

            renderBitmap.WritePixels(
                new Int32Rect(0, 0, item.Width, item.Height),
                pixels,
                item.Stride,
                0);
            renderBitmapSource = item;
        }

        return renderBitmap != null;
    }

    private static PixelFormat ToWpfPixelFormat(VisionPixelFormat pixelFormat)
        => pixelFormat switch
        {
            VisionPixelFormat.Mono8 => PixelFormats.Gray8,
            VisionPixelFormat.Bgr24 => PixelFormats.Bgr24,
            VisionPixelFormat.Bgra32 => PixelFormats.Bgra32,
            VisionPixelFormat.Rgb24 => PixelFormats.Rgb24,
            _ => throw new NotSupportedException($"Unsupported vision pixel format: {pixelFormat}.")
        };

    private void EnsureViewInitialized(VisionImagePanelItemViewModel item)
    {
        if (!viewInitialized)
        {
            FitToView(item);
        }
    }

    private void FitToView()
    {
        if (ImageItem is { } item)
        {
            FitToView(item);
            InvalidateVisual();
        }
    }

    private void FitToView(VisionImagePanelItemViewModel item)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1 || item.Width <= 0 || item.Height <= 0)
        {
            return;
        }

        const double padding = 24;
        double scaleX = Math.Max(0.01, (ActualWidth - padding * 2) / item.Width);
        double scaleY = Math.Max(0.01, (ActualHeight - padding * 2) / item.Height);
        scale = Math.Clamp(Math.Min(scaleX, scaleY), MinimumScale, MaximumScale);
        double viewWidth = item.Width * scale;
        double viewHeight = item.Height * scale;
        offset = new Vector((ActualWidth - viewWidth) / 2, (ActualHeight - viewHeight) / 2);
        viewInitialized = true;
    }

    private Rect GetImageViewRect(VisionImagePanelItemViewModel item)
        => new(offset.X, offset.Y, item.Width * scale, item.Height * scale);

    private Point ImageToView(VisionPoint point) => ImageToView(new Point(point.X, point.Y));

    private Point ImageToView(Point imagePoint)
        => new(offset.X + imagePoint.X * scale, offset.Y + imagePoint.Y * scale);

    private Point ViewToImage(Point viewPoint)
        => new((viewPoint.X - offset.X) / scale, (viewPoint.Y - offset.Y) / scale);

    private Point ClampImagePoint(Point point)
    {
        VisionImagePanelItemViewModel? item = ImageItem;
        if (item == null)
        {
            return point;
        }

        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, item.Width - 1)),
            Math.Clamp(point.Y, 0, Math.Max(0, item.Height - 1)));
    }

    private Rect? NormalizeAndClamp(Rect? rect)
        => rect.HasValue ? NormalizeAndClamp(rect.Value) : null;

    private Rect NormalizeAndClamp(Rect rect)
    {
        VisionImagePanelItemViewModel? item = ImageItem;
        Rect normalized = NormalizeRect(rect);
        if (item == null)
        {
            return normalized;
        }

        double left = Math.Clamp(normalized.Left, 0, Math.Max(0, item.Width - MinimumRoiSize));
        double top = Math.Clamp(normalized.Top, 0, Math.Max(0, item.Height - MinimumRoiSize));
        double right = Math.Clamp(normalized.Right, left + MinimumRoiSize, item.Width);
        double bottom = Math.Clamp(normalized.Bottom, top + MinimumRoiSize, item.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private static Rect NormalizeRect(Rect rect)
        => new(
            Math.Min(rect.Left, rect.Right),
            Math.Min(rect.Top, rect.Bottom),
            Math.Abs(rect.Width),
            Math.Abs(rect.Height));

    private void DrawBackground(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(22, 26, 31)), null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private void DrawEmpty(DrawingContext dc)
    {
        DrawText(dc, "无可显示图像", 13, Brushes.Gray, new Point(ActualWidth / 2, ActualHeight / 2), centered: true);
        DrawStatus(dc);
    }

    private void DrawLoading(DrawingContext dc, VisionImagePanelItemViewModel item)
    {
        DrawText(dc, "正在加载图像...", 13, Brushes.Gray, new Point(ActualWidth / 2, ActualHeight / 2), centered: true);
        DrawImageInfo(dc, item);
        DrawStatus(dc);
    }

    private void DrawOverlays(DrawingContext dc, VisionImagePanelItemViewModel item)
    {
        foreach (IVisionOverlayShape overlay in item.Overlays)
        {
            Pen pen = new(ToBrush(overlay.Color), Math.Max(1, overlay.Thickness));
            pen.Freeze();
            switch (overlay)
            {
                case OverlayLine line:
                    dc.DrawLine(pen, ImageToView(line.Line.Start), ImageToView(line.Line.End));
                    DrawLabel(dc, line.Line.Start, overlay.Label, pen.Brush);
                    break;
                case OverlayCircle circle:
                    dc.DrawEllipse(null, pen, ImageToView(circle.Circle.Center), circle.Circle.Radius * scale, circle.Circle.Radius * scale);
                    DrawLabel(dc, circle.Circle.Center, overlay.Label, pen.Brush);
                    break;
                case OverlayRectangle rectangle:
                    dc.DrawRectangle(null, pen, ImageRectToView(rectangle.Rectangle));
                    DrawLabel(dc, new VisionPoint(rectangle.Rectangle.X, rectangle.Rectangle.Y), overlay.Label, pen.Brush);
                    break;
                case OverlayContour contour:
                    DrawPolyline(dc, contour.Contour.Points, contour.Contour.IsClosed, pen);
                    if (contour.Contour.Points.Count > 0)
                    {
                        DrawLabel(dc, contour.Contour.Points[0], overlay.Label, pen.Brush);
                    }
                    break;
                case OverlayText text:
                    DrawLabel(dc, text.Position, text.Text, pen.Brush, text.FontSize);
                    break;
            }
        }
    }

    private void DrawPolyline(DrawingContext dc, IReadOnlyList<VisionPoint> points, bool isClosed, Pen pen)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            dc.DrawLine(pen, ImageToView(points[i - 1]), ImageToView(points[i]));
        }

        if (isClosed)
        {
            dc.DrawLine(pen, ImageToView(points[^1]), ImageToView(points[0]));
        }
    }

    private void DrawRoi(DrawingContext dc)
    {
        if (Roi is not Rect roi || roi.Width <= 0 || roi.Height <= 0)
        {
            return;
        }

        Rect viewRect = ImageRectToView(new VisionRectangle(roi.X, roi.Y, roi.Width, roi.Height));
        Brush fill = new SolidColorBrush(Color.FromArgb(36, 0, 156, 255));
        Pen pen = new(new SolidColorBrush(Color.FromRgb(0, 156, 255)), 1.5) { DashStyle = DashStyles.Dash };
        fill.Freeze();
        pen.Freeze();
        dc.DrawRectangle(fill, pen, viewRect);

        Brush handleFill = new SolidColorBrush(Color.FromRgb(245, 250, 255));
        Pen handlePen = new(new SolidColorBrush(Color.FromRgb(0, 156, 255)), 1);
        handleFill.Freeze();
        handlePen.Freeze();
        foreach ((_, Rect handle) in GetHandleRects(viewRect))
        {
            dc.DrawRectangle(handleFill, handlePen, handle);
        }
    }

    private IEnumerable<(RoiHitType Type, Rect Rect)> GetHandleRects(Rect viewRect)
    {
        Point topLeft = new(viewRect.Left, viewRect.Top);
        Point top = new(viewRect.Left + viewRect.Width / 2, viewRect.Top);
        Point topRight = new(viewRect.Right, viewRect.Top);
        Point right = new(viewRect.Right, viewRect.Top + viewRect.Height / 2);
        Point bottomRight = new(viewRect.Right, viewRect.Bottom);
        Point bottom = new(viewRect.Left + viewRect.Width / 2, viewRect.Bottom);
        Point bottomLeft = new(viewRect.Left, viewRect.Bottom);
        Point left = new(viewRect.Left, viewRect.Top + viewRect.Height / 2);

        yield return (RoiHitType.TopLeft, CreateHandle(topLeft));
        yield return (RoiHitType.Top, CreateHandle(top));
        yield return (RoiHitType.TopRight, CreateHandle(topRight));
        yield return (RoiHitType.Right, CreateHandle(right));
        yield return (RoiHitType.BottomRight, CreateHandle(bottomRight));
        yield return (RoiHitType.Bottom, CreateHandle(bottom));
        yield return (RoiHitType.BottomLeft, CreateHandle(bottomLeft));
        yield return (RoiHitType.Left, CreateHandle(left));
    }

    private static Rect CreateHandle(Point center)
        => new(center.X - HandleSize / 2, center.Y - HandleSize / 2, HandleSize, HandleSize);

    private Rect ImageRectToView(VisionRectangle rectangle)
    {
        Point topLeft = ImageToView(new Point(rectangle.X, rectangle.Y));
        return new Rect(topLeft.X, topLeft.Y, rectangle.Width * scale, rectangle.Height * scale);
    }

    private void DrawLabel(DrawingContext dc, VisionPoint point, string label, Brush brush, double fontSize = 11)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        Point p = ImageToView(point);
        DrawText(dc, label, Math.Max(9, fontSize), brush, new Point(p.X + 4, p.Y + 4));
    }

    private void DrawStatus(DrawingContext dc)
    {
        var text = CreateText(statusText, 11, Brushes.WhiteSmoke);
        Rect background = new(8, ActualHeight - text.Height - 14, text.Width + 16, text.Height + 8);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, background, 4, 4);
        dc.DrawText(text, new Point(background.X + 8, background.Y + 4));
    }

    private void DrawImageInfo(DrawingContext dc, VisionImagePanelItemViewModel item)
    {
        string title = string.IsNullOrWhiteSpace(item.NodeName) ? "Image" : item.NodeName;
        if (!string.IsNullOrWhiteSpace(item.PositionText))
        {
            title = $"{title}  {item.PositionText}";
        }

        string port = string.IsNullOrWhiteSpace(item.PortName) ? item.Direction : $"{item.Direction} / {item.PortName}";
        string info = $"{title}\n{port}\n{item.Summary}\nOverlay: {item.OverlayCount}";

        FormattedText text = CreateText(info, 11, Brushes.WhiteSmoke);
        Rect background = new(8, 8, Math.Min(text.Width + 18, Math.Max(120, ActualWidth - 16)), text.Height + 10);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, background, 4, 4);
        dc.DrawText(text, new Point(background.X + 9, background.Y + 5));
    }

    private void DrawText(DrawingContext dc, string text, double fontSize, Brush brush, Point point, bool centered = false)
    {
        FormattedText formatted = CreateText(text, fontSize, brush);
        Point resolved = centered
            ? new Point(point.X - formatted.Width / 2, point.Y - formatted.Height / 2)
            : point;
        dc.DrawText(formatted, resolved);
    }

    private FormattedText CreateText(string text, double fontSize, Brush brush)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void UpdatePixelStatus(Point viewPoint)
    {
        VisionImagePanelItemViewModel? item = ImageItem;
        if (item?.Pixels is not { Length: > 0 })
        {
            return;
        }

        Point imagePoint = ClampImagePoint(ViewToImage(viewPoint));
        int x = (int)Math.Floor(imagePoint.X);
        int y = (int)Math.Floor(imagePoint.Y);
        string value = GetPixelValue(item, x, y);
        statusText = $"X={x}, Y={y}, {value}, Zoom={scale * 100:F0}%";
        InvalidateVisual();
    }

    private void UpdateRoiStatus()
    {
        if (Roi is not Rect roi)
        {
            statusText = HelpText;
            return;
        }

        statusText = $"ROI X={roi.X:F0}, Y={roi.Y:F0}, W={roi.Width:F0}, H={roi.Height:F0}";
    }

    private static string GetPixelValue(VisionImagePanelItemViewModel item, int x, int y)
    {
        byte[]? pixels = item.Pixels;
        if (pixels is not { Length: > 0 })
        {
            return item.PixelFormat.ToString();
        }

        int index = y * item.Stride;
        return item.PixelFormat switch
        {
            VisionPixelFormat.Mono8 when index + x < pixels.Length
                => $"Gray={pixels[index + x]}",
            VisionPixelFormat.Bgr24 when index + x * 3 + 2 < pixels.Length
                => $"BGR=({pixels[index + x * 3]},{pixels[index + x * 3 + 1]},{pixels[index + x * 3 + 2]})",
            VisionPixelFormat.Rgb24 when index + x * 3 + 2 < pixels.Length
                => $"RGB=({pixels[index + x * 3]},{pixels[index + x * 3 + 1]},{pixels[index + x * 3 + 2]})",
            VisionPixelFormat.Bgra32 when index + x * 4 + 3 < pixels.Length
                => $"BGRA=({pixels[index + x * 4]},{pixels[index + x * 4 + 1]},{pixels[index + x * 4 + 2]},{pixels[index + x * 4 + 3]})",
            VisionPixelFormat.Rgba32 when index + x * 4 + 3 < pixels.Length
                => $"RGBA=({pixels[index + x * 4]},{pixels[index + x * 4 + 1]},{pixels[index + x * 4 + 2]},{pixels[index + x * 4 + 3]})",
            _ => item.PixelFormat.ToString()
        };
    }

    private static Brush ToBrush(VisionColor color)
    {
        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private enum InteractionMode
    {
        None,
        Pan,
        CreateRoi,
        MoveRoi,
        ResizeRoi
    }

    private enum RoiHitType
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
}

