using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Kwy.MVVM.Core;

/// <summary>
/// 支持高性能批量操作的 ObservableCollection。
/// 专为解决 WPF 中大量数据更新导致 UI 线程卡死的问题而设计。
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

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
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    /// <summary>
    /// 拦截属性变更通知 (如 Count, Item[])
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
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
            _suppressNotification = true;
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
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
            _suppressNotification = true;
            Clear();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
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
