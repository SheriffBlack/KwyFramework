using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.WPF.Rendering;
using Kwy.Vision.WPF.Interaction;
using Kwy.Vision.WPF.Models;
using Kwy.Vision.WPF.Viewport;
using KwyTemplate.Vision.ViewModels.Items;
using System.Globalization;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KwyTemplate.Vision.Controls;

public sealed class VisionImageViewer : FrameworkElement
{
    public static readonly DependencyProperty ImageItemProperty =
        DependencyProperty.Register(
            nameof(ImageItem),
            typeof(VisionImagePanelItemViewModel),
            typeof(VisionImageViewer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnImageItemChanged));

    public static readonly DependencyProperty RoiLayersProperty =
        DependencyProperty.Register(
            nameof(RoiLayers),
            typeof(IEnumerable),
            typeof(VisionImageViewer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRoiLayersChanged));

    public static readonly DependencyProperty SelectedRoiLayerProperty = DependencyProperty.Register(
        nameof(SelectedRoiLayer), typeof(VisionRoiLayer), typeof(VisionImageViewer),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty RoiCreationKindProperty = DependencyProperty.Register(
        nameof(RoiCreationKind), typeof(VisionRoiCreationKind), typeof(VisionImageViewer),
        new PropertyMetadata(VisionRoiCreationKind.Rectangle));

    private const string HelpText = "滚轮缩放，右键/中键平移，左键新建/编辑 ROI，Delete 删除 ROI，双击适配窗口";

    private readonly WriteableBitmapRenderer renderer = new();
    private readonly VisionViewport viewport = new();
    private readonly VisionRegionEditor roiEditor = new();
    private bool viewInitialized;
    private InteractionMode interactionMode;
    private VisionRoiHandle activeHitType = VisionRoiHandle.None;
    private Point dragStartView;
    private Point lastMouseView;
    private Point roiStartImage;
    private IVisionRegion? dragStartRoi;
    private VisionRoiCreationKind roiCreationKind;
    private string statusText = HelpText;
    private ImageSource? renderBitmap;
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

    public IEnumerable? RoiLayers
    {
        get => (IEnumerable?)GetValue(RoiLayersProperty);
        set => SetValue(RoiLayersProperty, value);
    }

    public VisionRoiLayer? SelectedRoiLayer
    {
        get => (VisionRoiLayer?)GetValue(SelectedRoiLayerProperty);
        set => SetValue(SelectedRoiLayerProperty, value);
    }

    public VisionRoiCreationKind RoiCreationKind
    {
        get => (VisionRoiCreationKind)GetValue(RoiCreationKindProperty);
        set => SetValue(RoiCreationKindProperty, value);
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
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        viewport.ZoomAt(mouse, factor);
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
        if (activeHitType == VisionRoiHandle.Body && ActiveRoi != null && SelectedRoiLayer?.IsLocked != true)
        {
            interactionMode = InteractionMode.MoveRoi;
            dragStartRoi = ActiveRoi;
        }
        else if (activeHitType != VisionRoiHandle.None && ActiveRoi != null && SelectedRoiLayer?.IsLocked != true)
        {
            interactionMode = InteractionMode.ResizeRoi;
            dragStartRoi = ActiveRoi;
        }
        else
        {
            interactionMode = InteractionMode.CreateRoi;
            roiStartImage = ClampImagePoint(ViewToImage(dragStartView));
            roiCreationKind = RoiCreationKind;
            SetActiveRoi(roiEditor.Create(roiCreationKind, roiStartImage, roiStartImage, GetImageSize()));
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
                viewport.Pan(current - lastMouseView);
                lastMouseView = current;
                InvalidateVisual();
                break;

            case InteractionMode.CreateRoi:
                SetActiveRoi(roiEditor.Create(roiCreationKind, roiStartImage, ClampImagePoint(ViewToImage(current)), GetImageSize()));
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
            UpdateRoiStatus();
        }

        interactionMode = InteractionMode.None;
        activeHitType = VisionRoiHandle.None;
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
            RemoveSelectedRoiLayer();
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

    private static void OnRoiLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (VisionImageViewer)d;
        viewer.UnsubscribeRoiLayers(e.OldValue as IEnumerable);
        viewer.SubscribeRoiLayers(e.NewValue as IEnumerable);
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
        if (dragStartRoi != null)
        {
            SetActiveRoi(roiEditor.Move(dragStartRoi, delta, GetImageSize()));
        }
    }

    private void ResizeRoi(Point currentView)
    {
        Point currentImage = ClampImagePoint(ViewToImage(currentView));
        if (dragStartRoi != null)
        {
            SetActiveRoi(roiEditor.Resize(dragStartRoi, activeHitType, currentImage, GetImageSize()));
        }
    }

    private VisionRoiHandle HitTestRoi(Point viewPoint)
        => ActiveRoi != null && SelectedRoiLayer?.IsVisible == true
            ? roiEditor.HitTest(ActiveRoi, viewPoint, ImageToView, ViewToImage, viewport.Scale)
            : VisionRoiHandle.None;

    private void UpdateCursor(VisionRoiHandle hitType)
    {
        Cursor = hitType switch
        {
            VisionRoiHandle.Body => Cursors.SizeAll,
            VisionRoiHandle.Rotation => Cursors.Hand,
            VisionRoiHandle.Radius => Cursors.SizeWE,
            VisionRoiHandle.Left or VisionRoiHandle.Right => Cursors.SizeWE,
            VisionRoiHandle.Top or VisionRoiHandle.Bottom => Cursors.SizeNS,
            VisionRoiHandle.TopLeft or VisionRoiHandle.BottomRight => Cursors.SizeNWSE,
            VisionRoiHandle.TopRight or VisionRoiHandle.BottomLeft => Cursors.SizeNESW,
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

        try
        {
            _ = WriteableBitmapRenderer.ToWpfPixelFormat(item.PixelFormat);
        }
        catch (NotSupportedException)
        {
            renderBitmap = null;
            renderBitmapSource = null;
            return false;
        }

        if (!ReferenceEquals(renderBitmapSource, item))
        {
            int requiredLength = checked(item.Stride * item.Height);
            if (pixels.Length < requiredLength)
            {
                renderBitmap = null;
                renderBitmapSource = null;
                return false;
            }

            renderBitmap = renderer.Render(pixels, item.Width, item.Height, item.Stride, item.PixelFormat);
            renderBitmapSource = item;
        }

        return renderBitmap != null;
    }

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

        viewport.Fit(new Size(item.Width, item.Height), RenderSize, 24);
        viewInitialized = true;
    }

    private Rect GetImageViewRect(VisionImagePanelItemViewModel item)
        => ImageRectToView(new VisionRectangle(0, 0, item.Width, item.Height));

    private Point ImageToView(VisionPoint point) => ImageToView(new Point(point.X, point.Y));

    private Point ImageToView(Point imagePoint)
        => viewport.ImageToView(imagePoint);

    private Point ViewToImage(Point viewPoint)
        => viewport.ViewToImage(viewPoint);

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

    private Size GetImageSize()
        => ImageItem is { } item ? new Size(item.Width, item.Height) : Size.Empty;

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
                    dc.DrawEllipse(null, pen, ImageToView(circle.Circle.Center), circle.Circle.Radius * viewport.Scale, circle.Circle.Radius * viewport.Scale);
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
        if (RoiLayers == null)
        {
            return;
        }

        double scale = Math.Max(viewport.Scale, 0.0001);
        foreach (object? value in RoiLayers)
        {
            if (value is not VisionRoiLayer { IsVisible: true } layer) continue;
            bool selected = ReferenceEquals(layer, SelectedRoiLayer);
            Brush fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)36 : (byte)16, 0, 156, 255));
            Pen pen = new(new SolidColorBrush(selected ? Color.FromRgb(0, 156, 255) : Color.FromRgb(80, 130, 160)),
                (selected ? 1.5 : 1) / scale) { DashStyle = selected ? DashStyles.Dash : DashStyles.Solid };
            fill.Freeze();
            pen.Freeze();
            dc.PushTransform(new MatrixTransform(viewport.Matrix));
            dc.DrawGeometry(fill, pen, VisionRegionGeometryFactory.Create(layer.Region));
            dc.Pop();
        }

        if (SelectedRoiLayer is not { IsVisible: true, IsLocked: false } selectedLayer)
        {
            return;
        }

        Brush handleFill = new SolidColorBrush(Color.FromRgb(245, 250, 255));
        Pen handlePen = new(new SolidColorBrush(Color.FromRgb(0, 156, 255)), 1);
        handleFill.Freeze();
        handlePen.Freeze();
        foreach ((_, Point point) in roiEditor.GetHandlePoints(selectedLayer.Region, ImageToView, viewport.Scale))
        {
            dc.DrawRectangle(handleFill, handlePen, roiEditor.GetHandleRect(point));
        }
    }

    private Rect ImageRectToView(VisionRectangle rectangle)
    {
        Point topLeft = ImageToView(new Point(rectangle.X, rectangle.Y));
        Point bottomRight = ImageToView(new Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height));
        return new Rect(topLeft, bottomRight);
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
        statusText = $"X={x}, Y={y}, {value}, Zoom={viewport.Scale * 100:F0}%";
        InvalidateVisual();
    }

    private void UpdateRoiStatus()
    {
        if (ActiveRoi is not RectangleRegion rectangle)
        {
            statusText = ActiveRoi == null ? HelpText : $"ROI {ActiveRoi.GetType().Name}";
            return;
        }

        Rect roi = new(rectangle.Rectangle.X, rectangle.Rectangle.Y, rectangle.Rectangle.Width, rectangle.Rectangle.Height);

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

    private IVisionRegion? ActiveRoi => SelectedRoiLayer?.Region;

    private void SetActiveRoi(IVisionRegion region)
    {
        if (SelectedRoiLayer != null)
        {
            if (!SelectedRoiLayer.IsLocked) SelectedRoiLayer.Region = region;
            return;
        }

        if (RoiLayers is IList list)
        {
            var layer = new VisionRoiLayer { Name = $"ROI {list.Count + 1}", Region = region };
            list.Add(layer);
            SelectedRoiLayer = layer;
        }
    }

    private void RemoveSelectedRoiLayer()
    {
        if (SelectedRoiLayer == null || SelectedRoiLayer.IsLocked || RoiLayers is not IList list) return;
        int index = list.IndexOf(SelectedRoiLayer);
        list.Remove(SelectedRoiLayer);
        SelectedRoiLayer = list.Count == 0 ? null : list[Math.Clamp(index, 0, list.Count - 1)] as VisionRoiLayer;
    }

    private void SubscribeRoiLayers(IEnumerable? layers)
    {
        if (layers is INotifyCollectionChanged collection) collection.CollectionChanged += OnRoiLayersCollectionChanged;
        SubscribeRoiLayerItems(layers);
    }

    private void UnsubscribeRoiLayers(IEnumerable? layers)
    {
        if (layers is INotifyCollectionChanged collection) collection.CollectionChanged -= OnRoiLayersCollectionChanged;
        UnsubscribeRoiLayerItems(layers);
    }

    private void OnRoiLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UnsubscribeRoiLayerItems(e.OldItems);
        SubscribeRoiLayerItems(e.NewItems);
        InvalidateVisual();
    }

    private void SubscribeRoiLayerItems(IEnumerable? items)
    {
        if (items == null) return;
        foreach (object? item in items)
            if (item is INotifyPropertyChanged changed) changed.PropertyChanged += OnRoiLayerPropertyChanged;
    }

    private void UnsubscribeRoiLayerItems(IEnumerable? items)
    {
        if (items == null) return;
        foreach (object? item in items)
            if (item is INotifyPropertyChanged changed) changed.PropertyChanged -= OnRoiLayerPropertyChanged;
    }

    private void OnRoiLayerPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private enum InteractionMode
    {
        None,
        Pan,
        CreateRoi,
        MoveRoi,
        ResizeRoi
    }

}

