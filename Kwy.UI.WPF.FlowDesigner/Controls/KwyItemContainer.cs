using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.FlowDesigner.Controls;

/// <summary>
/// 节点项容器，用于包装 KwyNode，负责节点在画布上的定位 (Location) 和选中状态 (IsSelected)。
/// </summary>
public class KwyItemContainer : ContentControl
{
    static KwyItemContainer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyItemContainer), new FrameworkPropertyMetadata(typeof(KwyItemContainer)));
    }

    // ── 坐标定位（Point 类型，对应 X/Y） ──
    public static readonly DependencyProperty LocationProperty =
        DependencyProperty.Register("Location", typeof(Point), typeof(KwyItemContainer),
            new FrameworkPropertyMetadata(default(Point), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLocationChanged));

    public Point Location
    {
        get => (Point)GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    private static void OnLocationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyItemContainer container)
        {
            var pos = (Point)e.NewValue;
            Canvas.SetLeft(container, pos.X);
            Canvas.SetTop(container, pos.Y);
        }
    }

    // ── 选中状态 ──
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register("IsSelected", typeof(bool), typeof(KwyItemContainer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // ── 实际尺寸（用于自动排版或边界计算） ──
    public static readonly DependencyProperty ActualSizeProperty =
        DependencyProperty.Register("ActualSize", typeof(Size), typeof(KwyItemContainer), new PropertyMetadata(default(Size)));

    public Size ActualSize
    {
        get => (Size)GetValue(ActualSizeProperty);
        set => SetValue(ActualSizeProperty, value);
    }

    #region Dragging Logic

    private Point lastLogicalMousePosition;
    private Point startLogicalMousePosition;
    private Point internalLocation; // 关键：记录逻辑上的“原始坐标”，不带吸附
    private bool isDragging;
    private bool isMovementStarted;

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var editor = FindParent<KwyEditor>(this);
        if (editor != null)
        {
            editor.SelectedItem = DataContext;

            startLogicalMousePosition = editor.GetLogicalPosition(e.GetPosition(editor));
            lastLogicalMousePosition = startLogicalMousePosition;
            internalLocation = Location;
            isDragging = true;
            isMovementStarted = false;

            CaptureMouse();
            // 请求焦点，否则 Delete 快捷键无法命中编辑器
            editor.Focus();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            var editor = FindParent<KwyEditor>(this);
            if (editor != null)
            {
                var currentVisualPos = e.GetPosition(editor);
                var currentLogicalPos = editor.GetLogicalPosition(currentVisualPos);

                if (!isMovementStarted)
                {
                    var diff = currentLogicalPos - startLogicalMousePosition;
                    // 阈值也根据缩放调整，或者保持逻辑单位
                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance / editor.ViewportScale ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance / editor.ViewportScale)
                    {
                        isMovementStarted = true;
                        lastLogicalMousePosition = currentLogicalPos;
                    }
                    return;
                }

                // 2. 计算位移并更新逻辑坐标 (逻辑坐标永远丝滑跟随鼠标)
                var offset = currentLogicalPos - lastLogicalMousePosition;
                internalLocation = new Point(internalLocation.X + offset.X, internalLocation.Y + offset.Y);
                lastLogicalMousePosition = currentLogicalPos;

                double newX = internalLocation.X;
                double newY = internalLocation.Y;

                // 3. 吸附对齐逻辑 (基于逻辑坐标计算视觉显示坐标)
                const double snapThreshold = 4.0;
                List<double> vLines = new();
                List<double> hLines = new();

                // 获取当前节点的边界
                double myW = ActualWidth;
                double myH = ActualHeight;

                // 临时存储吸附后的坐标
                double snappedX = newX;
                double snappedY = newY;
                bool snappedV = false;
                bool snappedH = false;

                foreach (var item in editor.Items)
                {
                    var container = editor.ItemContainerGenerator.ContainerFromItem(item) as KwyItemContainer;
                    if (container == null || container == this) continue;

                    double otherX = container.Location.X;
                    double otherY = container.Location.Y;
                    double otherW = container.ActualWidth;
                    double otherH = container.ActualHeight;

                    // --- 垂直吸附 (X 轴) ---
                    double[] myXEdges = { newX, newX + myW / 2, newX + myW };
                    double[] otherXEdges = { otherX, otherX + otherW / 2, otherX + otherW };

                    foreach (var myX in myXEdges)
                    {
                        foreach (var otX in otherXEdges)
                        {
                            if (Math.Abs(myX - otX) < snapThreshold && !snappedV)
                            {
                                snappedX = otX - (myX - newX);
                                vLines.Add(otX);
                                snappedV = true;
                            }
                        }
                    }

                    // --- 水平吸附 (Y 轴) ---
                    double[] myYEdges = { newY, newY + myH / 2, newY + myH };
                    double[] otherYEdges = { otherY, otherY + otherH / 2, otherY + otherH };

                    foreach (var myY in myYEdges)
                    {
                        foreach (var otY in otherYEdges)
                        {
                            if (Math.Abs(myY - otY) < snapThreshold && !snappedH)
                            {
                                snappedY = otY - (myY - newY);
                                hLines.Add(otY);
                                snappedH = true;
                            }
                        }
                    }
                }

                Location = new Point(snappedX, snappedY);

                // 更新编辑器辅助线显示
                editor.VerticalGuideLines = vLines.Count > 0 ? vLines : null;
                editor.HorizontalGuideLines = hLines.Count > 0 ? hLines : null;
            }
        }
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (isDragging)
        {
            isDragging = false;
            ReleaseMouseCapture();

            // 清除辅助线
            var editor = FindParent<KwyEditor>(this);
            if (editor != null)
            {
                editor.VerticalGuideLines = null;
                editor.HorizontalGuideLines = null;
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

    #endregion Dragging Logic
}