using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 适用于 <see cref="TextBox"/> 及包含 TextBox 的自定义控件（如 KwyInput）的自动滚动行为。
/// 监听 TextChanged → ScrollToEnd / scrollViewer.ScrollToVerticalOffset(max)。
/// </summary>
public sealed class AutoScrollTextBoxBehavior : AutoScrollBehaviorBase<Control>
{
    private TextBox? targetTextBox;
    private TextChangedEventHandler? changedHandler;

    // 让基类用实际 TextBox 查找 ScrollViewer（而非外层的 KwyInput 壳子）
    protected override DependencyObject GetScrollSource()
        => targetTextBox ?? (DependencyObject)AssociatedObject;

    protected override void Subscribe()
    {
        targetTextBox = ResolveTextBox(AssociatedObject);
        if (targetTextBox == null) return;
        changedHandler = (_, _) => RequestScroll();
        targetTextBox.TextChanged += changedHandler;
    }

    protected override void Unsubscribe()
    {
        if (targetTextBox == null || changedHandler == null) return;
        targetTextBox.TextChanged -= changedHandler;
        targetTextBox = null;
        changedHandler = null;
    }

    protected override void PerformScroll()
    {
        if (scrollViewer != null)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
        else
            targetTextBox?.ScrollToEnd();
    }

    // ── 工具 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 解析目标 TextBox：
    /// · TextBox 直接使用；
    /// · 自定义控件（KwyInput 等）通过 ContentPresenter 或视觉树查找内部 TextBox。
    /// </summary>
    private TextBox? ResolveTextBox(Control control)
    {
        if (control is TextBox tb) return tb;

        // 典型的 KwyInput 结构：Control → ContentPresenter → TextBox
        var presenter = FindVisualChild<ContentPresenter>(control);
        if (presenter != null)
        {
            if (presenter.Content is TextBox inner) return inner;
            if (presenter.Content is FrameworkElement fe)
                return FindVisualChild<TextBox>(fe);
        }

        // 兜底：直接遍历视觉树
        return FindVisualChild<TextBox>(control);
    }
}