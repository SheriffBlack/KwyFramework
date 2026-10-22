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
    private readonly ConcurrentDictionary<string, ViewCacheItem> cache = new();
    private readonly DispatcherTimer cleanupTimer;
    private bool isDisposed;

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
        get => cleanupTimer.Interval;
        set => cleanupTimer.Interval = value;
    }

    public ViewCacheManager()
    {
        // 创建定时器，检查过期视图
        cleanupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        cleanupTimer.Tick += OnCleanupTimerTick;
        cleanupTimer.Start();
    }

    public FrameworkElement GetOrCreateView(string viewName, Func<FrameworkElement> viewFactory, string? regionName = null)
    {
        if (string.IsNullOrEmpty(viewName))
            throw new ArgumentException("视图名称不能为空", nameof(viewName));

        if (viewFactory == null)
            throw new ArgumentNullException(nameof(viewFactory));

        // 1. 检查缓存中是否存在且有效
        if (cache.TryGetValue(viewName, out var cacheItem))
        {
            if (cacheItem.State == ViewCacheState.Destroyed)
            {
                cache.TryRemove(viewName, out _);
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

        cache.TryAdd(viewName, cacheItem);
        return view;
    }

    public void SetRegionName(string viewName, string regionName)
    {
        if (cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.RegionName = regionName;
        }
    }

    public void MarkAsActive(string viewName)
    {
        if (cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.MarkAsActive();
        }
    }

    public void MarkAsDeactive(string viewName)
    {
        if (cache.TryGetValue(viewName, out var cacheItem))
        {
            cacheItem.MarkAsDeactive();
            ViewCached?.Invoke(cacheItem);
        }
    }

    public void RemoveView(string viewName)
    {
        if (cache.TryRemove(viewName, out var cacheItem))
        {
            DestroyViewInternal(cacheItem);
        }
    }

    public void CleanupExpiredViews()
    {
        var expiredItems = cache.Values
            .Where(item => item.IsExpired && item.State == ViewCacheState.Cached)
            .ToList();

        foreach (var item in expiredItems)
        {
            if (cache.TryRemove(item.ViewName, out var removedItem))
            {
                DestroyViewInternal(removedItem);
            }
        }
    }

    public void ClearCache()
    {
        var allItems = cache.Values.ToList();
        cache.Clear();

        foreach (var item in allItems)
        {
            DestroyViewInternal(item);
        }
    }

    public ViewCacheItem? GetCacheItem(string viewName)
    {
        cache.TryGetValue(viewName, out var cacheItem);
        return cacheItem;
    }

    private void DestroyViewInternal(ViewCacheItem item)
    {
        var view = item.View;
        item.MarkAsDestroyed();

        ViewDestroyed?.Invoke(item);

        if (view == null)
        {
            return;
        }

        var dispatcher = view.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            DestroyViewOnUiThread(item, view);
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            DisposeViewLifetime(item, view, clearDataContext: false);
            return;
        }

        try
        {
            _ = dispatcher.BeginInvoke(() => DestroyViewOnUiThread(item, view), DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            // 应用关闭阶段 Dispatcher 可能已经不可访问，缓存释放不能阻塞进程退出。
            DisposeViewLifetime(item, view, clearDataContext: false);
        }
        catch (TaskCanceledException)
        {
            // Dispatcher 正在关闭时会取消排队任务，视图缓存销毁直接跳过剩余 UI 清理。
            DisposeViewLifetime(item, view, clearDataContext: false);
        }
    }

    private static void DestroyViewOnUiThread(ViewCacheItem item, FrameworkElement view)
    {
        DisposeViewLifetime(item, view, clearDataContext: true);
    }

    private static void DisposeViewLifetime(
        ViewCacheItem item,
        FrameworkElement view,
        bool clearDataContext)
    {
        var serviceScope = item.ServiceScope;
        item.ServiceScope = null;

        if (serviceScope != null)
        {
            // 作用域统一拥有并释放 View、ViewModel 及其 scoped/transient 依赖，
            // 避免手动 Dispose 后根容器在退出时再次 Dispose。
            serviceScope.Dispose();
            if (clearDataContext)
            {
                view.DataContext = null;
            }
            return;
        }

        if (view is IDisposable viewDisposable)
        {
            viewDisposable.Dispose();
        }

        if (view.DataContext is IDisposable vmDisposable)
        {
            vmDisposable.Dispose();
        }

        if (clearDataContext)
        {
            view.DataContext = null;
        }
    }

    private void OnCleanupTimerTick(object? sender, EventArgs e) => CleanupExpiredViews();

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        cleanupTimer.Stop();
        cleanupTimer.Tick -= OnCleanupTimerTick;
        ClearCache();
        GC.SuppressFinalize(this);
    }
}
