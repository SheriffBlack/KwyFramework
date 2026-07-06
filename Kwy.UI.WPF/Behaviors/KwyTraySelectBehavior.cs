using Kwy.UI.WPF.Controls;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static Kwy.UI.WPF.Controls.KwyTray;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// KwyTray 选择行为：支持单个点击和框选多个盘孔
/// </summary>
public class KwyTraySelectBehavior : Behavior<ItemsControl>
{
    private bool isDragging;
    private Point startPoint;
    private Rectangle? selectionRect;
    private Canvas? adornerCanvas;
    private const double DragThreshold = 5.0; // 拖拽阈值，超过此距离才认为是拖拽

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.PreviewMouseLeftButtonDown += OnMouseDown;
        AssociatedObject.PreviewMouseMove += OnMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp += OnMouseUp;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseLeftButtonDown -= OnMouseDown;
        AssociatedObject.PreviewMouseMove -= OnMouseMove;
        AssociatedObject.PreviewMouseLeftButtonUp -= OnMouseUp;

        // 释放鼠标捕获
        if (AssociatedObject.IsMouseCaptured)
        {
            AssociatedObject.ReleaseMouseCapture();
        }

        RemoveSelectionRectangle();
        base.OnDetaching();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        startPoint = e.GetPosition(AssociatedObject);

        // 检查点击的是否是盘孔（Border）
        var hitElement = e.OriginalSource as DependencyObject;
        var border = FindParent<Border>(hitElement);
        var container = FindParent<ContentPresenter>(hitElement);

        // 如果点击的是盘孔，让 ClickShowContextMenuBehavior 处理菜单弹出
        if (container != null && container.DataContext is ITrayItem item && border != null)
        {
            // 清除其他选择
            ClearAllSelection();
            // 选中点击的项
            item.IsSelected = true;
            // 不处理事件，让 ClickShowContextMenuBehavior 处理菜单弹出
            // 事件会继续传播到 Border，由 ClickShowContextMenuBehavior 处理
            return; // 不阻止事件，让 ClickShowContextMenuBehavior 处理
        }

        // 如果点击的不是盘孔（空白区域），准备开始框选（不立即创建矩形）
        isDragging = true;

        // 清除所有选择
        ClearAllSelection();

        // 确保装饰层存在
        EnsureAdornerCanvas();

        // 捕获鼠标，确保即使移出控件也能继续选择
        AssociatedObject.CaptureMouse();

        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging) return;

        var currentPoint = e.GetPosition(AssociatedObject);

        // 只有当超过拖拽阈值才创建并显示选择框
        var distance = Math.Sqrt(Math.Pow(currentPoint.X - startPoint.X, 2) +
                                 Math.Pow(currentPoint.Y - startPoint.Y, 2));
        if (distance < DragThreshold) return;

        // 超过阈值后才创建矩形（如果尚未创建）
        if (selectionRect == null && adornerCanvas != null)
        {
            CreateSelectionRectangle();
        }

        if (selectionRect != null)
        {
            UpdateSelectionRectangle(startPoint, currentPoint);
            UpdateSelectedItems();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging) return;

        isDragging = false;

        // 释放鼠标捕获
        AssociatedObject.ReleaseMouseCapture();

        var currentPoint = e.GetPosition(AssociatedObject);
        var distance = Math.Sqrt(Math.Pow(currentPoint.X - startPoint.X, 2) +
                                 Math.Pow(currentPoint.Y - startPoint.Y, 2));

        // 如果移动距离很小，清除选择框
        if (distance <= DragThreshold)
        {
            RemoveSelectionRectangle();
            ClearAllSelection();
            e.Handled = true;
            return;
        }

        // 框选完成
        var selectedCount = GetSelectedItemsCount();
        if (selectedCount > 0)
        {
            ShowContextMenu(AssociatedObject, currentPoint);
            // 注意：不调用 RemoveSelectionRectangle()，保持选择框显示
        }
        else
        {
            // 如果没有选中任何项，移除选择框
            RemoveSelectionRectangle();
        }

        e.Handled = true;
    }

    private void ClearAllSelection()
    {
        foreach (var item in AssociatedObject.Items)
        {
            if (item is ITrayItem plateItem)
            {
                plateItem.IsSelected = false;
            }
        }
    }

    private void UpdateSelectedItems()
    {
        if (selectionRect == null || adornerCanvas == null) return;

        // 获取选择矩形的边界
        double rectLeft = Canvas.GetLeft(selectionRect);
        double rectTop = Canvas.GetTop(selectionRect);

        Rect selectRect = new Rect(rectLeft, rectTop, selectionRect.Width, selectionRect.Height);

        foreach (var item in AssociatedObject.Items)
        {
            if (item is ITrayItem plateItem)
            {
                var container = AssociatedObject.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    // 获取盘孔 Border 的实际可视区域（而不是 ContentPresenter）
                    // 因为 Border 才是真正显示绿色/灰色的部分
                    var border = FindVisualChild<Border>(container);
                    if (border != null)
                    {
                        Rect itemRect = GetElementRect(border);

                        // 只有当选择矩形真正与盘孔的可视区域相交时才选中
                        // 使用 IntersectsWith 检查是否有真正的交集
                        bool intersects = selectRect.IntersectsWith(itemRect);

                        if (intersects)
                        {
                            // 计算交集矩形
                            Rect intersection = Rect.Intersect(selectRect, itemRect);

                            // 只有当交集面积大于盘孔面积的5%时才认为真正选中
                            // 这样可以避免边缘轻微接触导致的误选，同时保证正常选择
                            double itemArea = itemRect.Width * itemRect.Height;
                            double intersectionArea = intersection.Width * intersection.Height;

                            // 如果交集面积有效，检查比例
                            if (itemArea > 0 && intersectionArea > 0)
                            {
                                double intersectionRatio = intersectionArea / itemArea;
                                plateItem.IsSelected = intersectionRatio > 0.05; // 至少5%的交集
                            }
                            else
                            {
                                // 如果面积无效，使用简单的相交判断
                                plateItem.IsSelected = true;
                            }
                        }
                        else
                        {
                            plateItem.IsSelected = false;
                        }
                    }
                    else
                    {
                        // 如果找不到 Border，使用容器的矩形（备用方案）
                        Rect itemRect = GetElementRect(container);
                        plateItem.IsSelected = selectRect.IntersectsWith(itemRect);
                    }
                }
            }
        }
    }

    private Rect GetElementRect(FrameworkElement element)
    {
        try
        {
            // 获取元素相对于 KwyTray 控件的实际位置和大小
            var transform = element.TransformToAncestor(AssociatedObject);
            var topLeft = transform.Transform(new Point(0, 0));

            // 使用 ActualWidth 和 ActualHeight 获取实际渲染尺寸
            // 注意：如果元素还没有渲染，这些值可能为0
            if (element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                return new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
            }

            // 如果 ActualWidth/Height 为0，尝试使用 RenderSize
            if (element.RenderSize.Width > 0 && element.RenderSize.Height > 0)
            {
                return new Rect(topLeft, element.RenderSize);
            }

            // 最后的备用方案：使用 DesiredSize（可能不准确）
            return new Rect(topLeft, element.DesiredSize);
        }
        catch
        {
            // 如果转换失败，返回空矩形
            return new Rect();
        }
    }

    private void EnsureAdornerCanvas()
    {
        if (adornerCanvas != null) return;

        // 如果 AssociatedObject 是 KwyTray，使用其方法获取 Canvas
        if (AssociatedObject is KwyTray kwyTray)
        {
            adornerCanvas = kwyTray.GetSelectionCanvas();
        }

        if (adornerCanvas == null)
        {
            // 如果模板中没有，尝试从视觉树中查找
            adornerCanvas = FindVisualChild<Canvas>(AssociatedObject);
        }
    }

    private void CreateSelectionRectangle()
    {
        if (adornerCanvas == null) return;

        // 类似QQ截图的选择框样式
        selectionRect = new Rectangle
        {
            Stroke = AssociatedObject.TryFindResource("SelectionBorderBrush") as Brush,
            StrokeThickness = 2,
            Fill = AssociatedObject.TryFindResource("SelectionBackgroundBrush") as Brush,
            StrokeDashArray = new DoubleCollection { 4, 4 }, // 更明显的虚线
            Opacity = 0.8
        };

        adornerCanvas.Children.Add(selectionRect);
    }

    private void UpdateSelectionRectangle(Point p1, Point p2)
    {
        if (selectionRect == null || adornerCanvas == null) return;

        double x = Math.Min(p1.X, p2.X);
        double y = Math.Min(p1.Y, p2.Y);
        double w = Math.Abs(p1.X - p2.X);
        double h = Math.Abs(p1.Y - p2.Y);

        Canvas.SetLeft(selectionRect, x);
        Canvas.SetTop(selectionRect, y);
        selectionRect.Width = w;
        selectionRect.Height = h;
    }

    private void RemoveSelectionRectangle()
    {
        if (adornerCanvas != null && selectionRect != null)
        {
            adornerCanvas.Children.Remove(selectionRect);
            selectionRect = null;
        }
    }

    private void ShowContextMenu(FrameworkElement target, Point position)
    {
        // 查找 ContextMenu
        var contextMenu = FindContextMenu();
        if (contextMenu == null) return;

        contextMenu.PlacementTarget = target;
        contextMenu.Placement = PlacementMode.MousePoint;

        // 设置数据上下文为所有选中的项
        var selectedItems = GetSelectedItems().ToList();
        if (selectedItems.Count > 0)
        {
            contextMenu.DataContext = selectedItems;
        }

        // 监听菜单关闭事件，关闭时移除选择框
        contextMenu.Closed -= ContextMenu_Closed;
        contextMenu.Closed += ContextMenu_Closed;

        contextMenu.IsOpen = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        // 菜单关闭后，移除选择框
        RemoveSelectionRectangle();

        if (sender is ContextMenu contextMenu)
        {
            contextMenu.Closed -= ContextMenu_Closed;
        }
    }

    private ContextMenu? FindContextMenu()
    {
        // 从第一个选中的项获取 ContextMenu
        foreach (var item in AssociatedObject.Items)
        {
            if (item is ITrayItem plateItem && plateItem.IsSelected)
            {
                var container = AssociatedObject.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var border = FindVisualChild<Border>(container);
                    if (border?.ContextMenu != null)
                    {
                        return border.ContextMenu;
                    }
                }
            }
        }

        // 如果没有选中的项，尝试从第一个项获取
        if (AssociatedObject.Items.Count > 0)
        {
            var firstContainer = AssociatedObject.ItemContainerGenerator.ContainerFromItem(AssociatedObject.Items[0]) as FrameworkElement;
            if (firstContainer != null)
            {
                var border = FindVisualChild<Border>(firstContainer);
                return border?.ContextMenu;
            }
        }

        return null;
    }

    private IEnumerable<ITrayItem> GetSelectedItems()
    {
        foreach (var item in AssociatedObject.Items)
        {
            if (item is ITrayItem plateItem && plateItem.IsSelected)
            {
                yield return plateItem;
            }
        }
    }

    private int GetSelectedItemsCount()
    {
        int count = 0;
        foreach (var item in AssociatedObject.Items)
        {
            if (item is ITrayItem plateItem && plateItem.IsSelected)
            {
                count++;
            }
        }
        return count;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        var parentObject = child;
        while (parentObject != null)
        {
            if (parentObject is T parent)
            {
                return parent;
            }
            parentObject = LogicalTreeHelper.GetParent(parentObject) ??
                          VisualTreeHelper.GetParent(parentObject);
        }
        return null;
    }
}
