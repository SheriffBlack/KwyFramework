using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 自动滚动到底部行为基类（模板方法模式）
/// 封装：防抖定时器、用户是否手动滚动检测、ScrollViewer 事件监听
/// 子类只需实现三个抽象方法：Subscribe / Unsubscribe / PerformScroll
/// </summary>
public abstract class AutoScrollBehaviorBase<T> : Behavior<T> where T : Control
{
    private DispatcherTimer? scrollTimer;
    private bool isScrollingPending;

    /// <summary>子类可访问内部 ScrollViewer 以实现精细滚动</summary>
    protected ScrollViewer? scrollViewer;

    private bool userHasScrolled;
    private const double ScrollThreshold = 5.0;

    /// <summary>防抖延迟（ms），默认 100 ms；XAML 直接设置特性值即可</summary>
    public int DebounceDelay { get; set; } = 100;

    // ── 生命周期 ──────────────────────────────────────────────────────────

    protected override void OnAttached()
    {
        base.OnAttached();
        scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceDelay) };
        scrollTimer.Tick += OnScrollTimerTick;
        AssociatedObject.Loaded += OnLoaded;
        if (AssociatedObject.IsLoaded)
            Initialize();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Initialize();

    private void Initialize()
    {
        scrollViewer = FindVisualChild<ScrollViewer>(GetScrollSource());
        if (scrollViewer != null)
            scrollViewer.ScrollChanged += OnScrollChanged;
        Subscribe();
        PerformScroll();
    }

    protected override void OnDetaching()
    {
        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
            scrollViewer = null;
        }
        AssociatedObject.Loaded -= OnLoaded;
        if (scrollTimer != null)
        {
            scrollTimer.Tick -= OnScrollTimerTick;
            scrollTimer.Stop();
            scrollTimer = null;
        }
        Unsubscribe();
        base.OnDetaching();
    }

    // ── 模板方法（子类实现）──────────────────────────────────────────────

    /// <summary>
    /// 返回查找 ScrollViewer 的根对象。
    /// 默认为 AssociatedObject；TextBox 子类可返回内部实际的 TextBox。
    /// </summary>
    protected virtual DependencyObject GetScrollSource() => AssociatedObject;

    /// <summary>订阅内容变化事件（集合 / 文本）</summary>
    protected abstract void Subscribe();

    /// <summary>取消订阅（OnDetaching 时调用）</summary>
    protected abstract void Unsubscribe();

    /// <summary>执行实际的滚动操作</summary>
    protected abstract void PerformScroll();

    // ── 共享工具 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 子类在内容变化时调用此方法，触发防抖滚动。
    /// 如果用户已手动向上滚动则忽略。
    /// </summary>
    protected void RequestScroll()
    {
        if (userHasScrolled) return;
        isScrollingPending = true;
        if (scrollTimer != null && !scrollTimer.IsEnabled)
            scrollTimer.Start();
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        scrollTimer?.Stop();
        if (isScrollingPending)
        {
            PerformScroll();
            isScrollingPending = false;
        }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (scrollViewer == null) return;
        // 距底部超过阈值 → 标记用户已手动滚动，暂停自动追底
        userHasScrolled = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset > ScrollThreshold;
    }

    /// <summary>递归查找视觉树中第一个指定类型的子元素</summary>
    protected static TChild? FindVisualChild<TChild>(DependencyObject parent)
        where TChild : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TChild typed) return typed;
            var found = FindVisualChild<TChild>(child);
            if (found != null) return found;
        }
        return null;
    }
}