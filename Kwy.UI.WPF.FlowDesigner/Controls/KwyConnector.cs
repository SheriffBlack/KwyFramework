using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kwy.UI.Flow;

namespace Kwy.UI.WPF.FlowDesigner.Controls;

/// <summary>
/// 端口控件（输入/输出），支持锚点坐标同步、连接状态显示及自定义颜色。
/// </summary>
public class KwyConnector : HeaderedContentControl
{
    static KwyConnector()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyConnector), new FrameworkPropertyMetadata(typeof(KwyConnector)));
    }

    /// <summary>
    /// 端口在画布上的中心锚点坐标（由容器或布局逻辑更新）。
    /// </summary>
    public static readonly DependencyProperty AnchorProperty =
        DependencyProperty.Register("Anchor", typeof(Point), typeof(KwyConnector),
            new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public Point Anchor
    {
        get => (Point)GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    /// <summary>
    /// 是否已连接。
    /// </summary>
    public static readonly DependencyProperty IsConnectedProperty =
        DependencyProperty.Register("IsConnected", typeof(bool), typeof(KwyConnector), new PropertyMetadata(false, OnIsConnectedChanged));

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    private static void OnIsConnectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyConnector connector) connector.UpdateIsActivePin();
    }

    /// <summary>
    /// 当前生效的侧边。由应用层绑定到 ViewModel，用于在冗余引脚中决定连线锚点。
    /// </summary>
    public static readonly DependencyProperty ActiveSideProperty =
        DependencyProperty.Register("ActiveSide", typeof(FlowPortSide?), typeof(KwyConnector),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnActiveSideChanged));

    public FlowPortSide? ActiveSide
    {
        get => (FlowPortSide?)GetValue(ActiveSideProperty);
        set => SetValue(ActiveSideProperty, value);
    }

    private static void OnActiveSideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyConnector connector) connector.UpdateIsActivePin();
    }

    /// <summary>
    /// 端口分配的侧边 (Left, Top, Right, Bottom)。
    /// </summary>
    public static readonly DependencyProperty SideProperty =
        DependencyProperty.Register("Side", typeof(FlowPortSide), typeof(KwyConnector), new PropertyMetadata(FlowPortSide.Left, OnSideChanged));

    public FlowPortSide Side
    {
        get => (FlowPortSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    private static void OnSideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyConnector connector) connector.UpdateIsActivePin();
    }

    // ── 只读属性：是否为活跃引脚 ──────────────────────
    private static readonly DependencyPropertyKey IsActivePinPropertyKey =
        DependencyProperty.RegisterReadOnly("IsActivePin", typeof(bool), typeof(KwyConnector), new PropertyMetadata(false));

    public static readonly DependencyProperty IsActivePinProperty = IsActivePinPropertyKey.DependencyProperty;

    public bool IsActivePin
    {
        get => (bool)GetValue(IsActivePinProperty);
        private set => SetValue(IsActivePinPropertyKey, value);
    }

    private void UpdateIsActivePin()
    {
        // 核心逻辑：
        // 1. 如果 Side 匹配记录的 ActiveSide，那一定是活跃点。
        // 2. 如果 ActiveSide 是空的但是已经连接了，我们要选一个默认侧边显示，否则连线会指空。
        if (Side == ActiveSide)
        {
            IsActivePin = true;
            return;
        }

        // 初始加载或未明确选择时的回退方案：
        if (ActiveSide is null && IsConnected)
        {
            // 获取父级 ItemContainer 判断是输入还是输出方向（或者检查绑定的端口数据）
            // 这里简单根据 Side 字符串做默认分配：输入默认为 Left，输出默认为 Right
            if (Side is FlowPortSide.Left or FlowPortSide.Right)
            {
                IsActivePin = true;
                return;
            }
        }

        IsActivePin = false;
    }

    /// <summary>
    /// 端口主题颜色（用于同步端口圆点、文字标签颜色）。
    /// </summary>
    public static readonly DependencyProperty PortColorProperty =
        DependencyProperty.Register("PortColor", typeof(Brush), typeof(KwyConnector), new PropertyMetadata(Brushes.Gray));

    public Brush PortColor
    {
        get => (Brush)GetValue(PortColorProperty);
        set => SetValue(PortColorProperty, value);
    }

    /// <summary>
    /// 端口类型 (Data, Execution)。
    /// </summary>
    public static readonly DependencyProperty PortTypeProperty =
        DependencyProperty.Register("PortType", typeof(FlowPortType), typeof(KwyConnector), new PropertyMetadata(FlowPortType.Data));

    public FlowPortType PortType
    {
        get => (FlowPortType)GetValue(PortTypeProperty);
        set => SetValue(PortTypeProperty, value);
    }

    /// <summary>
    /// 端口方向 (Input, Output)。
    /// </summary>
    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register("Direction", typeof(FlowPortDirection), typeof(KwyConnector), new PropertyMetadata(FlowPortDirection.Input));

    public FlowPortDirection Direction
    {
        get => (FlowPortDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>
    /// 获取此端口所属的节点数据项。
    /// 该值由编辑器容器自动同步，只读且不依赖数据模型中的特定属性名称。
    /// </summary>
    private static readonly DependencyPropertyKey OwnerNodePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(OwnerNode),
            typeof(object),
            typeof(KwyConnector),
            new PropertyMetadata(null));

    public static readonly DependencyProperty OwnerNodeProperty = OwnerNodePropertyKey.DependencyProperty;

    public object? OwnerNode
    {
        get => GetValue(OwnerNodeProperty);
        private set => SetValue(OwnerNodePropertyKey, value);
    }

    public KwyConnector()
    {
        Loaded += (s, e) => UpdateOwnerNodeAndAnchor();
        DataContextChanged += (s, e) => UpdateOwnerNode();
        LayoutUpdated += (s, e) => ScheduleAnchorUpdate();
    }

    private Point _lastAnchor;
    private FrameworkElement? _portCircle;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _portCircle = Template.FindName("PART_PortCircle", this) as FrameworkElement;
        ScheduleAnchorUpdate();
    }

    private void UpdateOwnerNodeAndAnchor()
    {
        UpdateOwnerNode();
        ScheduleAnchorUpdate();
    }

    private void UpdateOwnerNode()
        => OwnerNode = FindParent<KwyItemContainer>(this)?.DataContext;

    private bool isAnchorUpdatePending;

    private void ScheduleAnchorUpdate()
    {
        if (isAnchorUpdatePending || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        isAnchorUpdatePending = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            isAnchorUpdatePending = false;
            UpdateAnchor();
        }));
    }

    private void UpdateAnchor()
    {
        if (IsLoaded && IsVisible)
        {
            var editor = FindParent<KwyEditor>(this);
            if (editor != null)
            {
                // 获取具体的圆点对象，如果没有命中则回退到自身
                var targetElement = _portCircle ?? this;
                var center = new Point(targetElement.ActualWidth / 2, targetElement.ActualHeight / 2);

                // 核心修复：先获取相对于 editor 的视觉位置，再通过 editor 转换为逻辑位置
                var visualAnchor = targetElement.TranslatePoint(center, editor);
                var logicalAnchor = editor.GetLogicalPosition(visualAnchor);

                // 策略：如果端口有多个物理引脚，只有当前选中的侧边（或未连接时）才更新逻辑坐标
                // 如果已经连接，且记录的侧边不是我，则我不提供坐标更新（防止连线跳变）
                if (IsConnected && ActiveSide is not null && ActiveSide != Side)
                    return;

                if (Math.Abs(logicalAnchor.X - _lastAnchor.X) > 0.1 || Math.Abs(logicalAnchor.Y - _lastAnchor.Y) > 0.1)
                {
                    _lastAnchor = logicalAnchor;
                    Anchor = logicalAnchor;
                }
            }
        }
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // 核心修复：标记为已处理，防止事件冒泡到 KwyItemContainer 触发节点拖拽
        e.Handled = true;

        var editor = FindParent<KwyEditor>(this);
        if (editor == null) return;

        if (!editor.IsEditingEnabled)
        {
            return;
        }

        if (editor.IsConnecting)
        {
            // 更新活跃侧边
            ActiveSide = Side;

            // 策略：如果已经在连线中，点击第二个端口则完成连线
            if (editor.ConnectionCompletedCommand != null)
            {
                // 使用结构化参数传递端口及其所属节点，避免调用方依赖元组顺序。
                var param = new FlowConnectionCompletedEventArgs(
                    editor.PendingSource,
                    editor.PendingSourceOwner,
                    DataContext,
                    OwnerNode);
                if (editor.CanConnect(param) && editor.ConnectionCompletedCommand.CanExecute(param))
                {
                    editor.ConnectionCompletedCommand.Execute(param);
                }
            }
            editor.EndConnecting();
        }
        else
        {
            // 更新活跃侧边
            ActiveSide = Side;

            // 策略：如果不在连线中，点击端口则开始连线
            var param = new FlowConnectorEventArgs(DataContext, OwnerNode);
            if (editor.ConnectionStartedCommand != null && editor.ConnectionStartedCommand.CanExecute(param))
            {
                editor.ConnectionStartedCommand.Execute(param);
                editor.StartConnecting(DataContext, OwnerNode, Anchor, Side);
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        var editor = FindParent<KwyEditor>(this);
        if (editor != null && editor.IsEditingEnabled && editor.IsConnecting)
        {
            // 策略：如果是从不同端口拖拽释放到此端口，则尝试完成连线
            if (editor.PendingSource != DataContext)
            {
                if (editor.ConnectionCompletedCommand != null)
                {
                    var param = new FlowConnectionCompletedEventArgs(
                        editor.PendingSource,
                        editor.PendingSourceOwner,
                        DataContext,
                        OwnerNode);
                    if (editor.CanConnect(param) && editor.ConnectionCompletedCommand.CanExecute(param))
                    {
                        editor.ConnectionCompletedCommand.Execute(param);
                    }
                }
                editor.EndConnecting();
                e.Handled = true;
            }
        }
    }
}
