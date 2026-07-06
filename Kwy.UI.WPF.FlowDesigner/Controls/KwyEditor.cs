using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Kwy.UI.WPF.FlowDesigner.Controls;

// ── 预览连线的数据模型 (ViewModel) ──
public class KwyPendingConnectionViewModel : INotifyPropertyChanged
{
    private Point _source;

    public Point Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    private Point _target;

    public Point Target
    {
        get => _target;
        set { _target = value; OnPropertyChanged(); }
    }

    private string _side = "Right";

    public string Side
    {
        get => _side;
        set { _side = value; OnPropertyChanged(); }
    }

    private string _targetSide = "Left";

    public string TargetSide
    {
        get => _targetSide;
        set { _targetSide = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── 连线辅助转换器 ──
public class KwyConnectionConverter : IValueConverter
{
    public static KwyConnectionConverter OffsetX { get; } = new KwyConnectionConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Point p && double.TryParse(parameter?.ToString(), out var offset))
        {
            return new Point(p.X + offset, p.Y);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// 流程图编辑器容器，支持节点定位、缩放及坐标系管理。
/// </summary>
public class KwyEditor : ItemsControl
{
    static KwyEditor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyEditor), new FrameworkPropertyMetadata(typeof(KwyEditor)));
    }

    #region Dependency Properties

    // ── MVVM 触发自动重排视口 ──
    public static readonly DependencyProperty FitToScreenTriggerProperty =
        DependencyProperty.RegisterAttached("FitToScreenTrigger", typeof(int), typeof(KwyEditor), new PropertyMetadata(0, OnFitToScreenTriggerChanged));

    public static int GetFitToScreenTrigger(DependencyObject obj) => (int)obj.GetValue(FitToScreenTriggerProperty);

    public static void SetFitToScreenTrigger(DependencyObject obj, int value) => obj.SetValue(FitToScreenTriggerProperty, value);

    private static void OnFitToScreenTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyEditor editor && e.NewValue is int newTrigger && newTrigger > 0)
        {
            // 给 WPF 布局引擎喘息时间，等待节点绑定坐标更新后再执行缩放匹配
            editor.Dispatcher.InvokeAsync(() => editor.FitToScreen(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    // ── 连线集合 ──
    public static readonly DependencyProperty ConnectionsProperty =
        DependencyProperty.Register("Connections", typeof(IEnumerable), typeof(KwyEditor), new PropertyMetadata(null));

    public IEnumerable? Connections
    {
        get => (IEnumerable?)GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    // ── 选中项 ──
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register("SelectedItem", typeof(object), typeof(KwyEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // ── 连线模板 ──
    public static readonly DependencyProperty ConnectionTemplateProperty =
        DependencyProperty.Register("ConnectionTemplate", typeof(DataTemplate), typeof(KwyEditor), new PropertyMetadata(null));

    public DataTemplate? ConnectionTemplate
    {
        get => (DataTemplate?)GetValue(ConnectionTemplateProperty);
        set => SetValue(ConnectionTemplateProperty, value);
    }

    // ── 交互命令：开始连接 ──
    public static readonly DependencyProperty ConnectionStartedCommandProperty =
        DependencyProperty.Register("ConnectionStartedCommand", typeof(ICommand), typeof(KwyEditor), new PropertyMetadata(null));

    public ICommand? ConnectionStartedCommand
    {
        get => (ICommand?)GetValue(ConnectionStartedCommandProperty);
        set => SetValue(ConnectionStartedCommandProperty, value);
    }

    // ── 交互命令：完成连接 ──
    public static readonly DependencyProperty ConnectionCompletedCommandProperty =
        DependencyProperty.Register("ConnectionCompletedCommand", typeof(ICommand), typeof(KwyEditor), new PropertyMetadata(null));

    public ICommand? ConnectionCompletedCommand
    {
        get => (ICommand?)GetValue(ConnectionCompletedCommandProperty);
        set => SetValue(ConnectionCompletedCommandProperty, value);
    }

    // ── 交互命令：断开连接 ──
    public static readonly DependencyProperty DisconnectConnectorCommandProperty =
        DependencyProperty.Register("DisconnectConnectorCommand", typeof(ICommand), typeof(KwyEditor), new PropertyMetadata(null));

    public ICommand? DisconnectConnectorCommand
    {
        get => (ICommand?)GetValue(DisconnectConnectorCommandProperty);
        set => SetValue(DisconnectConnectorCommandProperty, value);
    }

    // ── 预览连线数据对象 ──
    public static readonly DependencyProperty PendingConnectionProperty =
        DependencyProperty.Register("PendingConnection", typeof(object), typeof(KwyEditor), new PropertyMetadata(null));

    public object? PendingConnection
    {
        get => GetValue(PendingConnectionProperty);
        set => SetValue(PendingConnectionProperty, value);
    }

    // ── 预览起始端口 ──
    public static readonly DependencyProperty PendingSourceProperty =
        DependencyProperty.Register("PendingSource", typeof(object), typeof(KwyEditor), new PropertyMetadata(null));

    public object? PendingSource
    {
        get => GetValue(PendingSourceProperty);
        set => SetValue(PendingSourceProperty, value);
    }

    // ── 预览连线模板 ──
    public static readonly DependencyProperty PendingConnectionTemplateProperty =
        DependencyProperty.Register("PendingConnectionTemplate", typeof(DataTemplate), typeof(KwyEditor), new PropertyMetadata(null));

    public DataTemplate? PendingConnectionTemplate
    {
        get => (DataTemplate?)GetValue(PendingConnectionTemplateProperty);
        set => SetValue(PendingConnectionTemplateProperty, value);
    }

    // ── 吸附中的目标端口 ──
    public static readonly DependencyProperty SnappingTargetProperty =
        DependencyProperty.Register("SnappingTarget", typeof(object), typeof(KwyEditor), new PropertyMetadata(null));

    public object? SnappingTarget
    {
        get => GetValue(SnappingTargetProperty);
        set => SetValue(SnappingTargetProperty, value);
    }

    // ── 辅助对齐线 ──
    public static readonly DependencyProperty VerticalGuideLinesProperty =
        DependencyProperty.Register("VerticalGuideLines", typeof(IEnumerable), typeof(KwyEditor), new PropertyMetadata(null));

    public IEnumerable? VerticalGuideLines
    {
        get => (IEnumerable?)GetValue(VerticalGuideLinesProperty);
        set => SetValue(VerticalGuideLinesProperty, value);
    }

    public static readonly DependencyProperty HorizontalGuideLinesProperty =
        DependencyProperty.Register("HorizontalGuideLines", typeof(IEnumerable), typeof(KwyEditor), new PropertyMetadata(null));

    public IEnumerable? HorizontalGuideLines
    {
        get => (IEnumerable?)GetValue(HorizontalGuideLinesProperty);
        set => SetValue(HorizontalGuideLinesProperty, value);
    }

    // ── 视口缩放 ──
    public static readonly DependencyProperty ViewportScaleProperty =
        DependencyProperty.Register("ViewportScale", typeof(double), typeof(KwyEditor), new PropertyMetadata(1.0));

    public double ViewportScale
    {
        get => (double)GetValue(ViewportScaleProperty);
        set => SetValue(ViewportScaleProperty, value);
    }

    // ── 视口偏移 ──
    public static readonly DependencyProperty ViewportOffsetProperty =
        DependencyProperty.Register("ViewportOffset", typeof(Vector), typeof(KwyEditor), new PropertyMetadata(new Vector(0, 0)));

    public Vector ViewportOffset
    {
        get => (Vector)GetValue(ViewportOffsetProperty);
        set => SetValue(ViewportOffsetProperty, value);
    }

    #endregion Dependency Properties

    private Point panLastMousePosition;
    private bool isPanning;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Left)
        {
            Focus();
        }
        else if (e.ChangedButton == MouseButton.Middle)
        {
            panLastMousePosition = e.GetPosition(this);
            isPanning = true;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            isPanning = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        double oldScale = ViewportScale;
        double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
        double newScale = Math.Clamp(oldScale * zoomFactor, 0.1, 10.0);

        if (Math.Abs(newScale - oldScale) > 0.001)
        {
            Point mousePos = e.GetPosition(this);

            // 计算缩放中心点在当前逻辑坐标系下的位置
            // Logical = (Visual - Offset) / Scale
            Vector currentOffset = ViewportOffset;
            double focalX = (mousePos.X - currentOffset.X) / oldScale;
            double focalY = (mousePos.Y - currentOffset.Y) / oldScale;

            ViewportScale = newScale;

            // 更新偏移量以保持鼠标位置在缩放时不动
            // NewOffset = Visual - Logical * NewScale
            ViewportOffset = new Vector(
                mousePos.X - focalX * newScale,
                mousePos.Y - focalY * newScale
            );
        }

        e.Handled = true;
    }

    #region Pending Connection Logic

    public static readonly DependencyProperty IsConnectingProperty =
        DependencyProperty.Register("IsConnecting", typeof(bool), typeof(KwyEditor), new PropertyMetadata(false));

    public bool IsConnecting
    {
        get => (bool)GetValue(IsConnectingProperty);
        set => SetValue(IsConnectingProperty, value);
    }

    public void StartConnecting(object source, Point anchor, string? sourceSide = null)
    {
        PendingSource = source;
        PendingConnection = new KwyPendingConnectionViewModel
        {
            Source = anchor,
            Target = anchor,
            Side = sourceSide ?? "Right"
        };
        IsConnecting = true;
        CaptureMouse(); // 捕获鼠标，确保拖拽过程中即便移出控件也能接收到消息
    }

    public void EndConnecting()
    {
        PendingConnection = null;
        PendingSource = null;
        SnappingTarget = null;
        IsConnecting = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        Point visualPos = e.GetPosition(this);

        if (isPanning)
        {
            Vector diff = visualPos - panLastMousePosition;
            ViewportOffset += diff;
            panLastMousePosition = visualPos;
            return;
        }

        if (IsConnecting && PendingConnection is KwyPendingConnectionViewModel vm)
        {
            var logicalPos = GetLogicalPosition(visualPos);

            // ── 吸附逻辑 ──
            // 通过命中测试查找鼠标下方的端口。注意：必须使用视觉坐标进行命中测试！
            KwyConnector? snappedConnector = null;
            VisualTreeHelper.HitTest(this, null, (result) =>
            {
                var connector = FindParent<KwyConnector>(result.VisualHit);
                if (connector != null)
                {
                    snappedConnector = connector;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(visualPos));

            if (snappedConnector != null)
            {
                // 吸附到端口中心，同时把目标端口的方向也传入 vm，这样预览连线才能知道要从哪个方向进入
                vm.Target = snappedConnector.Anchor;
                vm.TargetSide = snappedConnector.Side ?? "Left";
                SnappingTarget = snappedConnector.DataContext;
            }
            else
            {
                // 自由移动：目标方向不明，重置为默认值
                vm.Target = logicalPos;
                vm.TargetSide = "Left";
                SnappingTarget = null;
            }
        }
    }

    private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        DependencyObject? parentObject = child;
        while (parentObject != null)
        {
            if (parentObject is T parent) return parent;
            parentObject = VisualTreeHelper.GetParent(parentObject);
        }
        return null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.Handled) return;

        base.OnMouseLeftButtonDown(e);

        // 关键逻辑：只有当点击的是编辑器背景本身时，才尝试处理连线或取消选中
        // 增加对 PART_TransformationLayer 内部背景点击的支持
        bool isBackgroundClick = e.OriginalSource == this ||
                                 e.OriginalSource is Canvas ||
                                 e.OriginalSource is Border ||
                                 e.OriginalSource is FrameworkElement fe && fe.Name == "PART_TransformationLayer" ||
                                 e.OriginalSource is Grid g && g.Background != null; // 命中我们添加的透明 Grid

        if (isBackgroundClick)
        {
            // 排除 ScrollBar 的干扰
            if (e.OriginalSource is DependencyObject dep && FindParent<System.Windows.Controls.Primitives.ScrollBar>(dep) != null)
                return;

            if (IsConnecting)
            {
                EndConnecting();
                e.Handled = true;
            }
            else
            {
                // 点击背景清空选中项 (确保没有命中 ItemContainer)
                if (FindParent<KwyItemContainer>(e.OriginalSource as DependencyObject) == null)
                {
                    SelectedItem = null;
                }
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (IsConnecting)
        {
            // 1. 如果在另一个有效的端口上释放，则完成连线 (Drag-and-Drop)
            if (SnappingTarget != null && SnappingTarget != PendingSource)
            {
                var param = (PendingSource, SnappingTarget);
                if (ConnectionCompletedCommand?.CanExecute(param) == true)
                {
                    ConnectionCompletedCommand.Execute(param);
                }
                EndConnecting();
                e.Handled = true;
            }
            // 2. 如果释放位置是起始端口，则保持“粘性模式” (释放捕获，但不结束 IsConnecting)
            else if (SnappingTarget == PendingSource)
            {
                ReleaseMouseCapture();
            }
            // 3. 如果在空白背景处长距离拖拽释放，则取消连线
            else if (SnappingTarget == null)
            {
                EndConnecting();
                e.Handled = true;
            }
        }
    }

    #endregion Pending Connection Logic

    #region Container Management

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new KwyItemContainer();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is KwyItemContainer;
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        if (IsConnecting && !IsPendingSourceStillInItems())
        {
            EndConnecting();
        }

        if (SelectedItem != null && !Items.Contains(SelectedItem))
        {
            SelectedItem = null;
        }
    }

    private bool IsPendingSourceStillInItems()
    {
        if (PendingSource == null)
        {
            return false;
        }

        if (Items.Contains(PendingSource))
        {
            return true;
        }

        var owner = PendingSource.GetType().GetProperty("Node")?.GetValue(PendingSource);
        return owner != null && Items.Contains(owner);
    }

    #endregion Container Management

    #region Coordinate Helpers

    /// <summary>
    /// 将屏幕/控件坐标转换为画布逻辑坐标
    /// </summary>
    public Point GetLogicalPosition(Point visualPoint)
    {
        return new Point(
            (visualPoint.X - ViewportOffset.X) / ViewportScale,
            (visualPoint.Y - ViewportOffset.Y) / ViewportScale
        );
    }

    /// <summary>
    /// 将画布逻辑坐标转换为屏幕/控件坐标
    /// </summary>
    public Point GetVisualPosition(Point logicalPoint)
    {
        return new Point(
            logicalPoint.X * ViewportScale + ViewportOffset.X,
            logicalPoint.Y * ViewportScale + ViewportOffset.Y
        );
    }

    #endregion Coordinate Helpers

    #region Viewport Helpers

    /// <summary>
    /// 调整视口缩放和平移，使所有节点完全显示在可视区域内
    /// </summary>
    public void FitToScreen()
    {
        if (Items.Count == 0) return;

        // 强制界面的所有绑定和内部布局刷新，这样后续才能准确拿到容器和真实的宽高
        UpdateLayout();

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var item in Items)
        {
            if (ItemContainerGenerator.ContainerFromItem(item) is KwyItemContainer container)
            {
                var loc = container.Location;
                // 如果刚刚生成，ActualWidth 可能仍未算出，因此需要强行更新或者给个适当默认值
                var width = container.ActualWidth > 0 ? container.ActualWidth : 150;
                var height = container.ActualHeight > 0 ? container.ActualHeight : 80;

                minX = Math.Min(minX, loc.X);
                minY = Math.Min(minY, loc.Y);
                maxX = Math.Max(maxX, loc.X + width);
                maxY = Math.Max(maxY, loc.Y + height);
            }
        }

        if (minX == double.MaxValue) return;

        double padding = 50; // 留出边缘空白
        double contentWidth = maxX - minX;
        double contentHeight = maxY - minY;

        double scaleX = (ActualWidth - padding * 2) / (contentWidth > 0 ? contentWidth : 1);
        double scaleY = (ActualHeight - padding * 2) / (contentHeight > 0 ? contentHeight : 1);

        double newScale = Math.Min(scaleX, scaleY);
        newScale = Math.Clamp(newScale, 0.1, 1.0); // 限制缩放级别 (不放大过头，最多原始尺寸 1.0)

        ViewportScale = newScale;
        ViewportOffset = new Vector(-minX * newScale + padding, -minY * newScale + padding);
    }

    #endregion Viewport Helpers
}
