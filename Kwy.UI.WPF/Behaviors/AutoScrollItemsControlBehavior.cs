using System.Collections.Specialized;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 适用于 <see cref="ItemsControl"/> 系列控件（DataGrid / ListBox / ListView）的自动滚动行为。
/// 监听 Items.CollectionChanged → ScrollIntoView(最后一项)。
/// 与虚拟化兼容：ScrollIntoView 会先 Realize 目标行再定位，不像直接改 ScrollViewer.Offset 那样偏移不准。
/// </summary>
public sealed class AutoScrollItemsControlBehavior : AutoScrollBehaviorBase<ItemsControl>
{
    private INotifyCollectionChanged? collectionSource;
    private NotifyCollectionChangedEventHandler? changedHandler;

    protected override void Subscribe()
    {
        if (AssociatedObject.Items is not INotifyCollectionChanged incc) return;
        collectionSource = incc;
        changedHandler = (_, _) => RequestScroll();
        incc.CollectionChanged += changedHandler;
    }

    protected override void Unsubscribe()
    {
        if (collectionSource == null || changedHandler == null) return;
        collectionSource.CollectionChanged -= changedHandler;
        collectionSource = null;
        changedHandler = null;
    }

    protected override void PerformScroll()
    {
        var items = AssociatedObject.Items;
        if (items.Count == 0) return;

        // 优先路径：直接操作 ScrollViewer，对所有 ItemsControl 子类通用
        if (scrollViewer != null)
        {
            scrollViewer.ScrollToBottom();
            return;
        }

        // 兜底路径：ScrollIntoView 定义在各子类上，分别调用
        var last = items[items.Count - 1];
        switch (AssociatedObject)
        {
            case DataGrid dg: dg.ScrollIntoView(last); break;
            case ListBox lb: lb.ScrollIntoView(last); break;
        }
    }
}