using Kwy.UI.Diagrams;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls.Diagrams;

/// <summary>
/// 通用二维图画布。
/// 绘制节点与连接关系，并提供选中、点击、缩放及平移的基础交互。
/// </summary>
public class KwyDiagramCanvas : Control
{
    private INotifyCollectionChanged? nodesNotifier;
    private INotifyCollectionChanged? connectionsNotifier;

    static KwyDiagramCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(typeof(KwyDiagramCanvas)));
    }

    /// <summary>
    /// 获取或设置节点集合。
    /// 当集合实现 <see cref="INotifyCollectionChanged"/> 时，画布会自动重绘。
    /// </summary>
    public IEnumerable? Nodes
    {
        get => (IEnumerable?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public static readonly DependencyProperty NodesProperty =
        DependencyProperty.Register(
            nameof(Nodes),
            typeof(IEnumerable),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(null, OnNodesChanged));

    /// <summary>
    /// 获取或设置连接关系集合。
    /// 当集合实现 <see cref="INotifyCollectionChanged"/> 时，画布会自动重绘。
    /// </summary>
    public IEnumerable? Connections
    {
        get => (IEnumerable?)GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    public static readonly DependencyProperty ConnectionsProperty =
        DependencyProperty.Register(
            nameof(Connections),
            typeof(IEnumerable),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(null, OnConnectionsChanged));

    /// <summary>
    /// 获取或设置当前选中的节点。
    /// </summary>
    public DiagramNode? SelectedNode
    {
        get => (DiagramNode?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public static readonly DependencyProperty SelectedNodeProperty =
        DependencyProperty.Register(
            nameof(SelectedNode),
            typeof(DiagramNode),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// 获取或设置画布缩放比例。
    /// </summary>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is double zoom && double.IsFinite(zoom) && zoom > 0);

    /// <summary>
    /// 获取或设置相对于左上角的逻辑平移量。
    /// </summary>
    public DiagramPoint PanOffset
    {
        get => (DiagramPoint)GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    public static readonly DependencyProperty PanOffsetProperty =
        DependencyProperty.Register(
            nameof(PanOffset),
            typeof(DiagramPoint),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(default(DiagramPoint), FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is DiagramPoint point
                && double.IsFinite(point.X)
                && double.IsFinite(point.Y));

    /// <summary>
    /// 获取或设置节点点击后执行的命令。
    /// 节点本身作为命令参数传入。
    /// </summary>
    public ICommand? NodeClickCommand
    {
        get => (ICommand?)GetValue(NodeClickCommandProperty);
        set => SetValue(NodeClickCommandProperty, value);
    }

    public static readonly DependencyProperty NodeClickCommandProperty =
        DependencyProperty.Register(
            nameof(NodeClickCommand),
            typeof(ICommand),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取或设置节点圆角半径。
    /// </summary>
    public CornerRadius NodeCornerRadius
    {
        get => (CornerRadius)GetValue(NodeCornerRadiusProperty);
        set => SetValue(NodeCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty NodeCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(NodeCornerRadius),
            typeof(CornerRadius),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(new CornerRadius(6d), FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is CornerRadius cornerRadius
                && double.IsFinite(cornerRadius.TopLeft) && cornerRadius.TopLeft >= 0
                && double.IsFinite(cornerRadius.TopRight) && cornerRadius.TopRight >= 0
                && double.IsFinite(cornerRadius.BottomRight) && cornerRadius.BottomRight >= 0
                && double.IsFinite(cornerRadius.BottomLeft) && cornerRadius.BottomLeft >= 0);

    /// <summary>
    /// 获取或设置半椭圆节点中矩形底座所占的比例。
    /// 默认保留较小的底座，使圆顶更接近设备模块的轮廓。
    /// </summary>
    public double SemiEllipseBaseRatio
    {
        get => (double)GetValue(SemiEllipseBaseRatioProperty);
        set => SetValue(SemiEllipseBaseRatioProperty, value);
    }

    public static readonly DependencyProperty SemiEllipseBaseRatioProperty =
        DependencyProperty.Register(
            nameof(SemiEllipseBaseRatio),
            typeof(double),
            typeof(KwyDiagramCanvas),
            new FrameworkPropertyMetadata(0.18d, FrameworkPropertyMetadataOptions.AffectsRender),
            static value => value is double ratio && double.IsFinite(ratio) && ratio >= 0d && ratio < 0.5d);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
        var nodes = GetNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        drawingContext.PushTransform(CreateViewportTransform());
        DrawConnections(drawingContext, nodes);
        DrawNodes(drawingContext, nodes);
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        DiagramNode? node = HitTestNode(ToLogicalPoint(e.GetPosition(this)));
        if (node is null)
        {
            return;
        }

        Focus();
        SelectedNode = node;
        if (NodeClickCommand?.CanExecute(node) == true)
        {
            NodeClickCommand.Execute(node);
        }

        e.Handled = true;
    }

    protected override void OnVisualParentChanged(DependencyObject? oldParent)
    {
        if (VisualParent is null)
        {
            DetachCollectionChangedHandlers();
        }
        else
        {
            AttachCollectionChangedHandlers();
        }

        base.OnVisualParentChanged(oldParent);
    }

    private static void OnNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (KwyDiagramCanvas)d;
        canvas.DetachNodesCollectionChangedHandler();
        canvas.AttachNodesCollectionChangedHandler();
        canvas.InvalidateVisual();
    }

    private static void OnConnectionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (KwyDiagramCanvas)d;
        canvas.DetachConnectionsCollectionChangedHandler();
        canvas.AttachConnectionsCollectionChangedHandler();
        canvas.InvalidateVisual();
    }

    private void AttachCollectionChangedHandlers()
    {
        AttachNodesCollectionChangedHandler();
        AttachConnectionsCollectionChangedHandler();
    }

    private void DetachCollectionChangedHandlers()
    {
        DetachNodesCollectionChangedHandler();
        DetachConnectionsCollectionChangedHandler();
    }

    private void AttachNodesCollectionChangedHandler()
    {
        if (nodesNotifier is null && Nodes is INotifyCollectionChanged notifier)
        {
            nodesNotifier = notifier;
            nodesNotifier.CollectionChanged += OnCollectionChanged;
        }
    }

    private void DetachNodesCollectionChangedHandler()
    {
        if (nodesNotifier is not null)
        {
            nodesNotifier.CollectionChanged -= OnCollectionChanged;
            nodesNotifier = null;
        }
    }

    private void AttachConnectionsCollectionChangedHandler()
    {
        if (connectionsNotifier is null && Connections is INotifyCollectionChanged notifier)
        {
            connectionsNotifier = notifier;
            connectionsNotifier.CollectionChanged += OnCollectionChanged;
        }
    }

    private void DetachConnectionsCollectionChangedHandler()
    {
        if (connectionsNotifier is not null)
        {
            connectionsNotifier.CollectionChanged -= OnCollectionChanged;
            connectionsNotifier = null;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    private Dictionary<string, DiagramNode> GetNodes()
    {
        var nodes = new Dictionary<string, DiagramNode>(StringComparer.Ordinal);
        if (Nodes is null)
        {
            return nodes;
        }

        foreach (object? item in Nodes)
        {
            if (item is not DiagramNode node
                || string.IsNullOrWhiteSpace(node.Id)
                || !IsValidNode(node))
            {
                continue;
            }

            nodes[node.Id] = node;
        }

        return nodes;
    }

    private void DrawConnections(DrawingContext drawingContext, IReadOnlyDictionary<string, DiagramNode> nodes)
    {
        if (Connections is null)
        {
            return;
        }

        foreach (object? item in Connections)
        {
            if (item is not DiagramConnection connection
                || !nodes.TryGetValue(connection.SourceId, out DiagramNode? source)
                || !nodes.TryGetValue(connection.TargetId, out DiagramNode? target))
            {
                continue;
            }

            double sourceThickness = double.IsFinite(connection.Thickness) && connection.Thickness > 0d
                ? connection.Thickness
                : 1d;
            double minimumThickness = connection.State == DiagramConnectionState.Active ? 2d : 1d;
            sourceThickness = Math.Max(sourceThickness, minimumThickness);
            double targetThickness = connection.TargetThickness is double configuredTargetThickness
                && double.IsFinite(configuredTargetThickness)
                && configuredTargetThickness > 0d
                ? configuredTargetThickness
                : sourceThickness;
            Brush brush = ResolveStateBrush(connection.State);

            if (connection.Shape == DiagramConnectionShape.Tapered)
            {
                DrawTaperedConnection(
                    drawingContext,
                    GetCenter(source),
                    GetCenter(target),
                    sourceThickness,
                    Math.Max(targetThickness, minimumThickness),
                    brush);
            }
            else
            {
                drawingContext.DrawLine(
                    new Pen(brush, sourceThickness),
                    GetCenter(source),
                    GetCenter(target));
            }
        }
    }

    private static void DrawTaperedConnection(
        DrawingContext drawingContext,
        Point source,
        Point target,
        double sourceThickness,
        double targetThickness,
        Brush brush)
    {
        Vector direction = target - source;
        if (direction.LengthSquared < double.Epsilon)
        {
            return;
        }

        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        Vector sourceOffset = perpendicular * (sourceThickness / 2d);
        Vector targetOffset = perpendicular * (targetThickness / 2d);
        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(source + sourceOffset, isFilled: true, isClosed: true);
        context.LineTo(target + targetOffset, isStroked: true, isSmoothJoin: false);
        context.LineTo(target - targetOffset, isStroked: true, isSmoothJoin: false);
        context.LineTo(source - sourceOffset, isStroked: true, isSmoothJoin: false);
        drawingContext.DrawGeometry(brush, null, geometry);
    }

    private void DrawNodes(DrawingContext drawingContext, IReadOnlyDictionary<string, DiagramNode> nodes)
    {
        foreach (DiagramNode node in nodes.Values)
        {
            bool isSelected = Equals(node, SelectedNode);
            Brush borderBrush = isSelected ? ResolveBrush("PrimaryBrush", Brushes.DodgerBlue) : ResolveStateBrush(node.State);
            double borderThickness = isSelected || node.State == DiagramNodeState.Active ? 2d : 1d;
            Rect bounds = new(node.X, node.Y, node.Width, node.Height);
            Geometry geometry = CreateNodeGeometry(node, bounds);
            bool useStateFill = node.UseStateFill || node.FillMode == DiagramNodeFillMode.State;
            Brush fillBrush = node.FillMode == DiagramNodeFillMode.SoftState
                ? ResolveSoftStateFillBrush(node.State)
                : useStateFill
                    ? ResolveStateFillBrush(node.State)
                    : ResolveBrush("ControlBackgroundBrush", Brushes.White);

            drawingContext.DrawGeometry(
                fillBrush,
                new Pen(borderBrush, borderThickness),
                geometry);

            if (!string.IsNullOrWhiteSpace(node.Label))
            {
                DrawLabel(drawingContext, node.Label, bounds, useStateFill);
            }
        }
    }

    private void DrawLabel(DrawingContext drawingContext, string label, Rect bounds, bool useStateFill)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formattedText = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            useStateFill ? Brushes.White : Foreground ?? ResolveBrush("ForegroundBrush", Brushes.Black),
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(0d, bounds.Width - 12d),
            MaxTextHeight = Math.Max(0d, bounds.Height - 8d),
            TextAlignment = TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis
        };

        drawingContext.DrawText(
            formattedText,
            new Point(
                bounds.X + 6d,
                bounds.Y + (bounds.Height - formattedText.Height) / 2d));
    }

    private DiagramNode? HitTestNode(DiagramPoint point)
        => GetNodes().Values.LastOrDefault(node => CreateNodeGeometry(
            node,
            new Rect(node.X, node.Y, node.Width, node.Height)).FillContains(new Point(point.X, point.Y)));

    private Geometry CreateNodeGeometry(DiagramNode node, Rect bounds)
    {
        Geometry geometry = node.Shape switch
        {
            DiagramNodeShape.Rectangle => new RectangleGeometry(bounds),
            DiagramNodeShape.Ellipse => new EllipseGeometry(bounds),
            DiagramNodeShape.SemiEllipse => CreateSemiEllipseGeometry(bounds, node.Orientation, SemiEllipseBaseRatio),
            DiagramNodeShape.Trapezoid => CreateTrapezoidGeometry(bounds),
            DiagramNodeShape.RegularPolygon => CreateRegularPolygonGeometry(bounds, node.PolygonSides),
            _ => CreateRoundedRectangleGeometry(bounds)
        };

        if (double.IsFinite(node.RotationAngle) && Math.Abs(node.RotationAngle) > double.Epsilon)
        {
            geometry.Transform = new RotateTransform(node.RotationAngle, bounds.X + (bounds.Width / 2d), bounds.Y + (bounds.Height / 2d));
        }

        return geometry;
    }

    private Geometry CreateRoundedRectangleGeometry(Rect bounds)
    {
        CornerRadius cornerRadius = NodeCornerRadius;
        double radius = Math.Min(cornerRadius.TopLeft, Math.Min(bounds.Width, bounds.Height) / 2d);
        return new RectangleGeometry(bounds, radius, radius);
    }

    private static Geometry CreateSemiEllipseGeometry(Rect bounds, DiagramNodeOrientation orientation, double baseRatio)
    {
        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();

        switch (orientation)
        {
            case DiagramNodeOrientation.Bottom:
            {
                double baseHeight = Math.Max(bounds.Height * baseRatio, bounds.Height - (bounds.Width / 2d));
                double baseY = bounds.Top + baseHeight;
                context.BeginFigure(new Point(bounds.Left, baseY), isFilled: true, isClosed: true);
                context.ArcTo(new Point(bounds.Right, baseY), new Size(bounds.Width / 2d, baseY - bounds.Top), 0d, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Right, bounds.Top), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Left, bounds.Top), isStroked: true, isSmoothJoin: false);
                break;
            }
            case DiagramNodeOrientation.Left:
            {
                double baseWidth = Math.Max(bounds.Width * baseRatio, bounds.Width - (bounds.Height / 2d));
                double baseX = bounds.Right - baseWidth;
                context.BeginFigure(new Point(baseX, bounds.Top), isFilled: true, isClosed: true);
                context.ArcTo(new Point(baseX, bounds.Bottom), new Size(baseX - bounds.Left, bounds.Height / 2d), 0d, isLargeArc: false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Right, bounds.Bottom), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Right, bounds.Top), isStroked: true, isSmoothJoin: false);
                break;
            }
            case DiagramNodeOrientation.Right:
            {
                double baseWidth = Math.Max(bounds.Width * baseRatio, bounds.Width - (bounds.Height / 2d));
                double baseX = bounds.Left + baseWidth;
                context.BeginFigure(new Point(baseX, bounds.Top), isFilled: true, isClosed: true);
                context.ArcTo(new Point(baseX, bounds.Bottom), new Size(bounds.Right - baseX, bounds.Height / 2d), 0d, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Left, bounds.Bottom), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Left, bounds.Top), isStroked: true, isSmoothJoin: false);
                break;
            }
            default:
            {
                double baseHeight = Math.Max(bounds.Height * baseRatio, bounds.Height - (bounds.Width / 2d));
                double baseY = bounds.Bottom - baseHeight;
                context.BeginFigure(new Point(bounds.Left, baseY), isFilled: true, isClosed: true);
                context.ArcTo(new Point(bounds.Right, baseY), new Size(bounds.Width / 2d, baseY - bounds.Top), 0d, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Right, bounds.Bottom), isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(bounds.Left, bounds.Bottom), isStroked: true, isSmoothJoin: false);
                break;
            }
        }
        return geometry;
    }

    private static Geometry CreateTrapezoidGeometry(Rect bounds)
    {
        double topInset = bounds.Width * 0.16d;
        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(new Point(bounds.Left + topInset, bounds.Top), isFilled: true, isClosed: true);
        context.LineTo(new Point(bounds.Right - topInset, bounds.Top), isStroked: true, isSmoothJoin: false);
        context.LineTo(new Point(bounds.Right, bounds.Bottom), isStroked: true, isSmoothJoin: false);
        context.LineTo(new Point(bounds.Left, bounds.Bottom), isStroked: true, isSmoothJoin: false);
        return geometry;
    }

    private static Geometry CreateRegularPolygonGeometry(Rect bounds, int sides)
    {
        int sideCount = Math.Max(3, sides);
        var geometry = new StreamGeometry();
        using StreamGeometryContext context = geometry.Open();
        Point center = new(bounds.Left + (bounds.Width / 2d), bounds.Top + (bounds.Height / 2d));
        double radiusX = bounds.Width / 2d;
        double radiusY = bounds.Height / 2d;

        for (int index = 0; index < sideCount; index++)
        {
            double angle = (-90d + ((360d / sideCount) * index)) * Math.PI / 180d;
            Point point = new(center.X + (radiusX * Math.Cos(angle)), center.Y + (radiusY * Math.Sin(angle)));
            if (index == 0)
            {
                context.BeginFigure(point, isFilled: true, isClosed: true);
            }
            else
            {
                context.LineTo(point, isStroked: true, isSmoothJoin: false);
            }
        }

        return geometry;
    }

    private Transform CreateViewportTransform()
    {
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(PanOffset.X, PanOffset.Y));
        transform.Children.Add(new ScaleTransform(Zoom, Zoom));
        return transform;
    }

    private DiagramPoint ToLogicalPoint(Point point)
        => new((point.X / Zoom) - PanOffset.X, (point.Y / Zoom) - PanOffset.Y);

    private Brush ResolveStateBrush(DiagramNodeState state)
        => state switch
        {
            DiagramNodeState.Active => ResolveBrush("PrimaryBrush", Brushes.DodgerBlue),
            DiagramNodeState.Disabled => ResolveBrush("ControlDisabledForegroundBrush", Brushes.Gray),
            DiagramNodeState.Warning => ResolveBrush("WarningBrush", Brushes.Orange),
            DiagramNodeState.Error => ResolveBrush("ErrorBrush", Brushes.IndianRed),
            _ => ResolveBrush("ControlBorderBrush", Brushes.SlateGray)
        };

    private Brush ResolveStateFillBrush(DiagramNodeState state)
        => state switch
        {
            DiagramNodeState.Active => ResolveBrush("PrimaryBrush", Brushes.DodgerBlue),
            DiagramNodeState.Disabled => ResolveBrush("ControlDisabledForegroundBrush", Brushes.Gray),
            DiagramNodeState.Warning => ResolveBrush("WarningBrush", Brushes.Orange),
            DiagramNodeState.Error => ResolveBrush("ErrorBrush", Brushes.IndianRed),
            _ => ResolveBrush("PrimaryBrush", Brushes.DodgerBlue)
        };

    private Brush ResolveSoftStateFillBrush(DiagramNodeState state)
        => state switch
        {
            DiagramNodeState.Disabled => ResolveBrush("ControlDisabledBackgroundBrush", Brushes.LightGray),
            DiagramNodeState.Warning => ResolveBrush("StateWarningBackgroundBrush", Brushes.Moccasin),
            DiagramNodeState.Error => ResolveBrush("StateErrorBackgroundBrush", Brushes.MistyRose),
            _ => ResolveBrush("ControlSelectedBackgroundBrush", Brushes.LightBlue)
        };

    private Brush ResolveStateBrush(DiagramConnectionState state)
        => state switch
        {
            DiagramConnectionState.Active => ResolveBrush("PrimaryBrush", Brushes.DodgerBlue),
            DiagramConnectionState.Disabled => ResolveBrush("ControlDisabledForegroundBrush", Brushes.Gray),
            DiagramConnectionState.Warning => ResolveBrush("WarningBrush", Brushes.Orange),
            DiagramConnectionState.Error => ResolveBrush("ErrorBrush", Brushes.IndianRed),
            _ => ResolveBrush("ControlBorderBrush", Brushes.SlateGray)
        };

    private Brush ResolveBrush(string resourceKey, Brush fallback)
        => TryFindResource(resourceKey) as Brush ?? fallback;

    private static bool IsValidNode(DiagramNode node)
        => double.IsFinite(node.X)
           && double.IsFinite(node.Y)
           && double.IsFinite(node.Width)
           && double.IsFinite(node.Height)
           && double.IsFinite(node.RotationAngle)
           && node.Width > 0
           && node.Height > 0;

    private static Point GetCenter(DiagramNode node)
        => new(node.X + (node.Width / 2d), node.Y + (node.Height / 2d));
}
