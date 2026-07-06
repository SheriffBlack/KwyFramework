using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// DataGridRow 行为辅助类，提供控制行悬停和选中行为的方法。
/// </summary>
public static class DataGridRowHelper
{
    // --- IsHoverSuppressed ---
    // 被内部元素通知，当前行是否应该抑制悬停效果
    public static readonly DependencyProperty IsHoverSuppressedProperty =
        DependencyProperty.RegisterAttached("IsHoverSuppressed", typeof(bool), typeof(DataGridRowHelper), new PropertyMetadata(false));

    public static bool GetIsHoverSuppressed(DependencyObject obj) => (bool)obj.GetValue(IsHoverSuppressedProperty);
    public static void SetIsHoverSuppressed(DependencyObject obj, bool value) => obj.SetValue(IsHoverSuppressedProperty, value);

    // --- SuppressRowHover ---
    // 允许内部任何元素（如单元格内的Button）在鼠标悬停时，抑制其所在 DataGridRow 的颜色高亮效果
    public static readonly DependencyProperty SuppressRowHoverProperty =
        DependencyProperty.RegisterAttached("SuppressRowHover", typeof(bool), typeof(DataGridRowHelper), new PropertyMetadata(false, OnSuppressRowHoverChanged));

    public static bool GetSuppressRowHover(DependencyObject obj) => (bool)obj.GetValue(SuppressRowHoverProperty);
    public static void SetSuppressRowHover(DependencyObject obj, bool value) => obj.SetValue(SuppressRowHoverProperty, value);

    private static readonly DependencyProperty CachedRowProperty =
        DependencyProperty.RegisterAttached("CachedRow", typeof(DataGridRow), typeof(DataGridRowHelper));

    private static void OnSuppressRowHoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                // 绑定生命周期以进行预计算
                element.Loaded += Element_Loaded;
                element.Unloaded += Element_Unloaded;
                element.MouseEnter += Element_MouseEnter;
                element.MouseLeave += Element_MouseLeave;
            }
            else
            {
                // 卸载时彻底清理
                element.Loaded -= Element_Loaded;
                element.Unloaded -= Element_Unloaded;
                element.MouseEnter -= Element_MouseEnter;
                element.MouseLeave -= Element_MouseLeave;
                element.ClearValue(CachedRowProperty);
            }
        }
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            // 【性能压榨核心】在加载时（且仅执行一次）往上查找 VisualTree，并缓存到控件本身。
            // WPF 的 DataGrid 虚拟化只会复用物理行并改变 DataContext，物理树结构不会变！
            // 这使得鼠标悬停的高频事件的计算复杂度从 O(N) 降为 O(1)。
            var row = FindParent<DataGridRow>(element);
            if (row != null)
            {
                element.SetValue(CachedRowProperty, row);
            }
        }
    }

    private static void Element_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            // 解除引用，防止虚拟化回收时的内存泄漏
            element.ClearValue(CachedRowProperty);
        }
    }

    private static void Element_MouseEnter(object sender, MouseEventArgs e)
    {
        // 极速读取 O(1) 并修改标记，零装箱拆箱开销，不会抢占渲染线程时间片
        if (sender is FrameworkElement element && element.GetValue(CachedRowProperty) is DataGridRow row)
        {
            SetIsHoverSuppressed(row, true);
        }
    }

    private static void Element_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.GetValue(CachedRowProperty) is DataGridRow row)
        {
            SetIsHoverSuppressed(row, false);
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }
}
