using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.WPF.Interaction;
using Kwy.Vision.WPF.Models;
using Kwy.Vision.WPF.Rendering;
using Kwy.Vision.WPF.Viewport;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kwy.Vision.WPF.Controls;

/// <summary>
/// Backend-independent WPF vision canvas with reusable bitmap rendering, pan/zoom,
/// coordinate conversion, and common overlay rendering.
/// </summary>
public sealed class VisionImageCanvas : FrameworkElement, IDisposable
{
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
        nameof(Image), typeof(IVisionImage), typeof(VisionImageCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnImageChanged));

    public static readonly DependencyProperty OverlaysProperty = DependencyProperty.Register(
        nameof(Overlays), typeof(IEnumerable), typeof(VisionImageCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AutoFitProperty = DependencyProperty.Register(
        nameof(AutoFit), typeof(bool), typeof(VisionImageCanvas), new PropertyMetadata(true));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(VisionImageCanvas),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(22, 26, 31)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RoiLayersProperty = DependencyProperty.Register(
        nameof(RoiLayers), typeof(IEnumerable), typeof(VisionImageCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRoiLayersChanged));

    public static readonly DependencyProperty SelectedRoiLayerProperty = DependencyProperty.Register(
        nameof(SelectedRoiLayer), typeof(VisionRoiLayer), typeof(VisionImageCanvas),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsRoiEditingEnabledProperty = DependencyProperty.Register(
        nameof(IsRoiEditingEnabled), typeof(bool), typeof(VisionImageCanvas), new PropertyMetadata(false));

    public static readonly DependencyProperty RoiCreationKindProperty = DependencyProperty.Register(
        nameof(RoiCreationKind), typeof(VisionRoiCreationKind), typeof(VisionImageCanvas),
        new PropertyMetadata(VisionRoiCreationKind.Rectangle));

    private readonly WriteableBitmapRenderer renderer = new();
    private readonly VisionViewport viewport = new();
    private readonly VisionRegionEditor roiEditor = new();
    private CancellationTokenSource? loadCancellation;
    private WriteableBitmap? bitmap;
    private bool fitPending = true;
    private bool panning;
    private bool disposed;
    private Point lastPointer;
    private Point roiDragStartView;
    private Point roiCreateStartImage;
    private IVisionRegion? roiDragStart;
    private VisionRoiHandle activeRoiHandle;
    private RoiInteraction roiInteraction;

    public VisionImageCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public IVisionImage? Image
    {
        get => (IVisionImage?)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public IEnumerable? Overlays
    {
        get => (IEnumerable?)GetValue(OverlaysProperty);
        set => SetValue(OverlaysProperty, value);
    }

    public bool AutoFit
    {
        get => (bool)GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
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

    public bool IsRoiEditingEnabled
    {
        get => (bool)GetValue(IsRoiEditingEnabledProperty);
        set => SetValue(IsRoiEditingEnabledProperty, value);
    }

    public VisionRoiCreationKind RoiCreationKind
    {
        get => (VisionRoiCreationKind)GetValue(RoiCreationKindProperty);
        set => SetValue(RoiCreationKindProperty, value);
    }

    public Matrix ViewMatrix => viewport.Matrix;

    public Point ImageToView(Point imagePoint) => viewport.ImageToView(imagePoint);

    public Point ViewToImage(Point viewPoint) => viewport.ViewToImage(viewPoint);

    public void FitToView()
    {
        if (Image is not { Width: > 0, Height: > 0 } image)
        {
            return;
        }

        viewport.Fit(new Size(image.Width, image.Height), RenderSize, 12);
        fitPending = false;
        InvalidateVisual();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
        bitmap = null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
        if (bitmap == null || Image == null)
        {
            return;
        }

        if (fitPending && AutoFit)
        {
            FitToView();
        }

        drawingContext.PushTransform(new MatrixTransform(viewport.Matrix));
        drawingContext.DrawImage(bitmap, new Rect(0, 0, Image.Width, Image.Height));
        DrawOverlays(drawingContext);
        drawingContext.Pop();
        DrawRoiLayers(drawingContext);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (AutoFit)
        {
            fitPending = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (bitmap == null)
        {
            return;
        }

        viewport.ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.15 : 1 / 1.15);
        fitPending = false;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ClickCount >= 2)
        {
            FitToView();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton is MouseButton.Right or MouseButton.Middle)
        {
            panning = true;
            lastPointer = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && IsRoiEditingEnabled && Image != null)
        {
            BeginRoiInteraction(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!panning)
        {
            if (roiInteraction != RoiInteraction.None)
            {
                UpdateRoiInteraction(e.GetPosition(this));
                e.Handled = true;
            }
            else if (IsRoiEditingEnabled)
            {
                UpdateRoiCursor(e.GetPosition(this));
            }
            return;
        }

        Point current = e.GetPosition(this);
        viewport.Pan(current - lastPointer);
        lastPointer = current;
        fitPending = false;
        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (panning)
        {
            panning = false;
            ReleaseMouseCapture();
            Cursor = null;
            e.Handled = true;
        }
        else if (roiInteraction != RoiInteraction.None)
        {
            roiInteraction = RoiInteraction.None;
            activeRoiHandle = VisionRoiHandle.None;
            ReleaseMouseCapture();
            Cursor = Cursors.Cross;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsRoiEditingEnabled && e.Key is Key.Delete or Key.Back)
        {
            if (SelectedRoiLayer != null && !SelectedRoiLayer.IsLocked)
            {
                RemoveSelectedLayer();
            }
            InvalidateVisual();
            e.Handled = true;
        }
    }

    private static void OnImageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (VisionImageCanvas)sender;
        canvas.fitPending = true;
        canvas.BeginLoadImage(e.NewValue as IVisionImage);
    }

    private static void OnRoiLayersChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (VisionImageCanvas)sender;
        canvas.UnsubscribeLayers(e.OldValue as IEnumerable);
        canvas.SubscribeLayers(e.NewValue as IEnumerable);
        canvas.InvalidateVisual();
    }

    private async void BeginLoadImage(IVisionImage? image)
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        CancellationToken token = loadCancellation.Token;

        if (image == null)
        {
            bitmap = null;
            InvalidateVisual();
            return;
        }

        try
        {
            ReadOnlyMemory<byte> pixels = await image.GetPixelMemoryAsync(token).ConfigureAwait(true);
            if (token.IsCancellationRequested || disposed || !ReferenceEquals(Image, image))
            {
                return;
            }

            bitmap = renderer.Render(pixels, image.Width, image.Height, image.Stride, image.PixelFormat);
            InvalidateVisual();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void DrawOverlays(DrawingContext context)
    {
        if (Overlays == null)
        {
            return;
        }

        double scale = Math.Max(viewport.Scale, 0.0001);
        foreach (object? value in Overlays)
        {
            if (value is not IVisionOverlayShape overlay)
            {
                continue;
            }

            Brush brush = new SolidColorBrush(Color.FromArgb(overlay.Color.A, overlay.Color.R, overlay.Color.G, overlay.Color.B));
            brush.Freeze();
            var pen = new Pen(brush, Math.Max(0.5, overlay.Thickness) / scale);
            pen.Freeze();

            switch (overlay)
            {
                case OverlayLine line:
                    context.DrawLine(pen, ToPoint(line.Line.Start), ToPoint(line.Line.End));
                    break;
                case OverlayCircle circle:
                    context.DrawEllipse(null, pen, ToPoint(circle.Circle.Center), circle.Circle.Radius, circle.Circle.Radius);
                    break;
                case OverlayRectangle rectangle:
                    context.DrawRectangle(null, pen, ToRect(rectangle.Rectangle));
                    break;
                case OverlayContour contour when contour.Contour.Points.Count > 1:
                    DrawContour(context, contour.Contour, pen);
                    break;
                case OverlayText text:
                    DrawText(context, text, brush, scale);
                    break;
            }
        }
    }

    private void BeginRoiInteraction(Point viewPoint)
    {
        Focus();
        CaptureMouse();
        roiDragStartView = viewPoint;
        activeRoiHandle = HitTestRoi(viewPoint);
        if (activeRoiHandle == VisionRoiHandle.Body && ActiveRegion != null && SelectedRoiLayer?.IsLocked != true)
        {
            roiInteraction = RoiInteraction.Move;
            roiDragStart = ActiveRegion;
        }
        else if (activeRoiHandle != VisionRoiHandle.None && ActiveRegion != null && SelectedRoiLayer?.IsLocked != true)
        {
            roiInteraction = RoiInteraction.Resize;
            roiDragStart = ActiveRegion;
        }
        else
        {
            roiInteraction = RoiInteraction.Create;
            roiCreateStartImage = ClampImagePoint(ViewToImage(viewPoint));
            SetActiveRegion(roiEditor.Create(RoiCreationKind, roiCreateStartImage, roiCreateStartImage, GetImageSize()));
        }
    }

    private void UpdateRoiInteraction(Point viewPoint)
    {
        Point imagePoint = ClampImagePoint(ViewToImage(viewPoint));
        IVisionRegion? updated = roiInteraction switch
        {
            RoiInteraction.Create => roiEditor.Create(RoiCreationKind, roiCreateStartImage, imagePoint, GetImageSize()),
            RoiInteraction.Move when roiDragStart != null => roiEditor.Move(
                roiDragStart,
                imagePoint - ViewToImage(roiDragStartView),
                GetImageSize()),
            RoiInteraction.Resize when roiDragStart != null => roiEditor.Resize(
                roiDragStart, activeRoiHandle, imagePoint, GetImageSize()),
            _ => ActiveRegion
        };
        if (updated != null)
        {
            SetActiveRegion(updated);
        }
        InvalidateVisual();
    }

    private VisionRoiHandle HitTestRoi(Point viewPoint)
        => ActiveRegion != null && SelectedRoiLayer?.IsVisible == true
            ? roiEditor.HitTest(ActiveRegion, viewPoint, ImageToView, ViewToImage, viewport.Scale)
            : VisionRoiHandle.None;

    private void UpdateRoiCursor(Point viewPoint)
    {
        Cursor = HitTestRoi(viewPoint) switch
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

    private void DrawRoiLayers(DrawingContext context)
    {
        if (RoiLayers == null)
        {
            return;
        }

        double scale = Math.Max(viewport.Scale, 0.0001);
        foreach (object? value in RoiLayers)
        {
            if (value is not VisionRoiLayer { IsVisible: true } layer)
            {
                continue;
            }

            bool selected = ReferenceEquals(layer, SelectedRoiLayer);
            var fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)42 : (byte)20, 0, 156, 255));
            var pen = new Pen(
                new SolidColorBrush(selected ? Color.FromRgb(0, 190, 255) : Color.FromRgb(80, 130, 160)),
                (selected ? 1.5 : 1) / scale)
            {
                DashStyle = selected ? DashStyles.Dash : DashStyles.Solid
            };
            fill.Freeze();
            pen.Freeze();
            context.PushTransform(new MatrixTransform(viewport.Matrix));
            context.DrawGeometry(fill, pen, VisionRegionGeometryFactory.Create(layer.Region));
            context.Pop();
        }

        if (!IsRoiEditingEnabled || SelectedRoiLayer is not { IsVisible: true, IsLocked: false } selectedLayer)
        {
            return;
        }

        var handleFill = new SolidColorBrush(Color.FromRgb(245, 250, 255));
        var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 156, 255)), 1);
        handleFill.Freeze();
        handlePen.Freeze();
        foreach ((_, Point point) in roiEditor.GetHandlePoints(selectedLayer.Region, ImageToView, viewport.Scale))
        {
            context.DrawRectangle(handleFill, handlePen, roiEditor.GetHandleRect(point));
        }
    }

    private Rect ImageRectToView(Rect rectangle)
        => new(ImageToView(rectangle.TopLeft), ImageToView(rectangle.BottomRight));

    private Point ClampImagePoint(Point point)
    {
        Size size = GetImageSize();
        return new Point(
            Math.Clamp(point.X, 0, Math.Max(0, size.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, size.Height)));
    }

    private Size GetImageSize()
        => Image is { } image ? new Size(image.Width, image.Height) : new Size(0, 0);

    private IVisionRegion? ActiveRegion => SelectedRoiLayer?.Region;

    private void SetActiveRegion(IVisionRegion region)
    {
        if (SelectedRoiLayer != null)
        {
            if (!SelectedRoiLayer.IsLocked)
            {
                SelectedRoiLayer.Region = region;
            }
            return;
        }

        if (RoiLayers is IList list)
        {
            var layer = new VisionRoiLayer { Name = $"ROI {list.Count + 1}", Region = region };
            list.Add(layer);
            SelectedRoiLayer = layer;
        }
    }

    private void RemoveSelectedLayer()
    {
        if (SelectedRoiLayer == null || RoiLayers is not IList list)
        {
            return;
        }

        VisionRoiLayer layer = SelectedRoiLayer;
        int index = list.IndexOf(layer);
        list.Remove(layer);
        SelectedRoiLayer = list.Count == 0 ? null : list[Math.Clamp(index, 0, list.Count - 1)] as VisionRoiLayer;
    }

    private void SubscribeLayers(IEnumerable? layers)
    {
        if (layers is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnLayersCollectionChanged;
        }
        SubscribeLayerItems(layers);
    }

    private void UnsubscribeLayers(IEnumerable? layers)
    {
        if (layers is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnLayersCollectionChanged;
        }
        UnsubscribeLayerItems(layers);
    }

    private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UnsubscribeLayerItems(e.OldItems);
        SubscribeLayerItems(e.NewItems);
        InvalidateVisual();
    }

    private void SubscribeLayerItems(IEnumerable? items)
    {
        if (items == null) return;
        foreach (object? item in items)
            if (item is INotifyPropertyChanged changed) changed.PropertyChanged += OnLayerPropertyChanged;
    }

    private void UnsubscribeLayerItems(IEnumerable? items)
    {
        if (items == null) return;
        foreach (object? item in items)
            if (item is INotifyPropertyChanged changed) changed.PropertyChanged -= OnLayerPropertyChanged;
    }

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();


    private static void DrawContour(DrawingContext context, VisionContour contour, Pen pen)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext sink = geometry.Open())
        {
            sink.BeginFigure(ToPoint(contour.Points[0]), false, contour.IsClosed);
            sink.PolyLineTo(contour.Points.Skip(1).Select(ToPoint).ToArray(), true, true);
        }

        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawText(DrawingContext context, OverlayText text, Brush brush, double scale)
    {
        var formatted = new FormattedText(
            text.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            Math.Max(1, text.FontSize) / scale,
            brush,
            1);
        context.DrawText(formatted, ToPoint(text.Position));
    }

    private static Point ToPoint(VisionPoint point) => new(point.X, point.Y);

    private static Rect ToRect(VisionRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private enum RoiInteraction
    {
        None,
        Create,
        Move,
        Resize
    }
}
