using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace Kwy.MVVM.WPF.Regions;

/// <summary>
/// 视图缓存管理器实现。
/// 已经被集成到 Kwy.MVVM.WPF 核心框架中，提供自动的区域视图缓存支持。
/// </summary>
public class ViewCacheManager : IViewCacheManager, IDisposable
{
    private readonly ConcurrentDictionary<string, ViewCacheItem> _cache = new();
    private readonly DispatcherTimer _cleanupTimer;
    private bool _isDisposed;

    public event Action<ViewCacheItem>? ViewCached;

    public event Action<ViewCacheItem>? ViewRestored;

    public event Action<ViewCacheItem>? ViewDestroyed;

    /// <summary>
    /// 默认缓存过期时间（1 分钟）。
    /// </summary>
    public TimeSpan DefaultCacheExpiration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 清理检查的时间间隔。
    /// </summary>
    public TimeSpan CleanupInterval
    {
        get => _cleanupTimer.Interval;
        set => _cleanupTimer.Interval = value;
    }

    public ViewCacheManager()
    {
        // 创建定时器，检查过期视图
        _cleanupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _cleanupTimer.Tick += OnCleanupTimerTick;
        _cleanupTimer.Start();
    }

    public FrameworkElement GetOrCreateView(string viewName, Func<FrameworkElement> viewFactory, string? regionName = null)
    {
        if (string.IsNullOrEmpty(viewName))
            throw new ArgumentException("视图名称不能为空", nameof(viewName));

        if (viewFactory == null)
            throw new ArgumentNullException(nameof(viewFactory));

        // 1. 检查缓存中是否存在且有效
        if (_cache.TryGetValue(viewName, out var cacheItem))
        {
            if (cacheItem.State == ViewCacheState.Destroyed)
            {
                _cache.TryRemove(viewName, out _);
            }
            else
            {
                // 视图存在，标记激活并返回
                cacheItem.MarkAsActive();
                if (!string.IsNullOrEmpty(regionName))
                {
                    cacheItem.RegionName = regionName;
                }

                ViewRestored?.Invoke(cacheItem);
                return cacheItem.View!;
            }
        }

        // 2. 缓存不存在，创建新视图
        var view = viewFactory();
        if (view == null)
            throw new InvalidOperationException($"视图工厂返回 null，视图名称：{viewName}");

        cacheItem = new ViewCacheItem
        {
            ViewName = viewName,
            View = view,
            RegionName = regionName,
            CacheExpiration = DefaultCacheExpiration
        };
        cacheItem.MarkAsActive();

        _cache.TryAdd(viewName, cacheItem);
        return view;
    }

    public void SetRegionName(string viewName, string regionName)
    {
        if (_cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.RegionName = regionName;
        }
    }

    public void MarkAsActive(string viewName)
    {
        if (_cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.MarkAsActive();
        }
    }

    public void MarkAsDeactive(string viewName)
    {
        if (_cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.MarkAsDeactive();
            ViewCached?.Invoke(cacheItem);
        }
    }

    public void RemoveView(string viewName)
    {
        if (_cache.TryRemove(viewName, out var cacheItem))
        {
            DestroyViewInternal(cacheItem);
        }
    }

    public void CleanupExpiredViews()
    {
        var expiredItems = _cache.Values
            .Where(item => item.IsExpired && item.State == ViewCacheState.Cached)
            .ToList();

        foreach (var item in expiredItems)
        {
            if (_cache.TryRemove(item.ViewName, out var removedItem))
            {
                DestroyViewInternal(removedItem);
            }
        }
    }

    public void ClearCache()
    {
        var allItems = _cache.Values.ToList();
        _cache.Clear();

        foreach (var item in allItems)
        {
            DestroyViewInternal(item);
        }
    }

    public ViewCacheItem? GetCacheItem(string viewName)
    {
        _cache.TryGetValue(viewName, out var cacheItem);
        return cacheItem;
    }

    private void DestroyViewInternal(ViewCacheItem item)
    {
        var view = item.View;
        item.MarkAsDestroyed();

        ViewDestroyed?.Invoke(item);

        if (view != null)
        {
            if (view is IDisposable viewDisposable) viewDisposable.Dispose();

            if (view.DataContext is IDisposable vmDisposable) vmDisposable.Dispose();

            view.DataContext = null;
        }
    }

    private void OnCleanupTimerTick(object? sender, EventArgs e) => CleanupExpiredViews();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _cleanupTimer.Stop();
        _cleanupTimer.Tick -= OnCleanupTimerTick;
        ClearCache();
        GC.SuppressFinalize(this);
    }
}
