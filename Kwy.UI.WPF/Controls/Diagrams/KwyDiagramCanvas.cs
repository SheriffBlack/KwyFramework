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

            drawingContext.DrawLine(
                new Pen(ResolveStateBrush(connection.State), connection.State == DiagramConnectionState.Active ? 2d : 1d),
                GetCenter(source),
                GetCenter(target));
        }
    }

    private void DrawNodes(DrawingContext drawingContext, IReadOnlyDictionary<string, DiagramNode> nodes)
    {
        foreach (DiagramNode node in nodes.Values)
        {
            bool isSelected = Equals(node, SelectedNode);
            Brush borderBrush = isSelected ? ResolveBrush("PrimaryBrush", Brushes.DodgerBlue) : ResolveStateBrush(node.State);
            double borderThickness = isSelected || node.State == DiagramNodeState.Active ? 2d : 1d;
            Rect bounds = new(node.X, node.Y, node.Width, node.Height);
            CornerRadius cornerRadius = NodeCornerRadius;

            drawingContext.DrawRoundedRectangle(
                ResolveBrush("ControlBackgroundBrush", Brushes.White),
                new Pen(borderBrush, borderThickness),
                bounds,
                Math.Min(cornerRadius.TopLeft, Math.Min(node.Width, node.Height) / 2d),
                Math.Min(cornerRadius.TopLeft, Math.Min(node.Width, node.Height) / 2d));

            if (!string.IsNullOrWhiteSpace(node.Label))
            {
                DrawLabel(drawingContext, node.Label, bounds);
            }
        }
    }

    private void DrawLabel(DrawingContext drawingContext, string label, Rect bounds)
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formattedText = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground ?? ResolveBrush("ForegroundBrush", Brushes.Black),
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
                bounds.X + (bounds.Width - formattedText.Width) / 2d,
                bounds.Y + (bounds.Height - formattedText.Height) / 2d));
    }

    private DiagramNode? HitTestNode(DiagramPoint point)
        => GetNodes().Values.LastOrDefault(node => new Rect(node.X, node.Y, node.Width, node.Height).Contains(new Point(point.X, point.Y)));

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
           && node.Width > 0
           && node.Height > 0;

    private static Point GetCenter(DiagramNode node)
        => new(node.X + (node.Width / 2d), node.Y + (node.Height / 2d));
}
