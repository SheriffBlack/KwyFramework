using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Kwy.MVVM.Core;

/// <summary>
/// 支持高性能批量操作的 ObservableCollection。
/// 用于减少大量数据更新产生的集合通知和 UI 刷新。
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool suppressNotification;

    public BulkObservableCollection() : base()
    {
    }

    public BulkObservableCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    /// <summary>
    /// 拦截集合变更通知
    /// </summary>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <summary>
    /// 拦截属性变更通知 (如 Count, Item[])
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }

    /// <summary>
    /// 高性能批量添加数据。仅在添加完成后触发一次 UI 刷新。
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        try
        {
            suppressNotification = true;
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            suppressNotification = false;
            RaiseResetNotifications();
        }
    }

    /// <summary>
    /// 高性能批量清空并添加新数据 (常用于刷新整个表格)
    /// </summary>
    public void Reset(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        try
        {
            suppressNotification = true;
            Clear();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            suppressNotification = false;
            RaiseResetNotifications();
        }
    }

    private void RaiseResetNotifications()
    {
        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
