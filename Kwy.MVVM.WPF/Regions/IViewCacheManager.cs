using System.Windows;

namespace Kwy.MVVM.WPF.Regions;

/// <summary>
/// 视图缓存状态
/// </summary>
public enum ViewCacheState
{
    Active,
    Cached,
    Destroyed
}

/// <summary>
/// 视图缓存项
/// </summary>
public class ViewCacheItem
{
    public string ViewName { get; set; } = string.Empty;
    public FrameworkElement? View { get; set; }
    public string? RegionName { get; set; }
    public ViewCacheState State { get; set; }
    public DateTime LastActiveTime { get; set; }
    public DateTime LastDeactiveTime { get; set; }
    public TimeSpan CacheExpiration { get; set; }
    public bool IsExpired => (DateTime.Now - LastDeactiveTime) > CacheExpiration;

    public void MarkAsActive()
    {
        State = ViewCacheState.Active;
        LastActiveTime = DateTime.Now;
    }

    public void MarkAsDeactive()
    {
        State = ViewCacheState.Cached;
        LastDeactiveTime = DateTime.Now;
    }

    public void MarkAsDestroyed()
    {
        State = ViewCacheState.Destroyed;
        View = null;
    }
}

/// <summary>
/// 视图缓存管理器接口。
/// 在 Kwy.MVVM.WPF 中定义，以便区域管理器 (RegionManager) 能自动享受视图缓存。
/// </summary>
public interface IViewCacheManager
{
    /// <summary>
    /// 当视图进入缓存时触发。
    /// </summary>
    event Action<ViewCacheItem>? ViewCached;

    /// <summary>
    /// 当视图从缓存中恢复到活跃状态时触发。
    /// </summary>
    event Action<ViewCacheItem>? ViewRestored;

    /// <summary>
    /// 当视图因过期或手动清理而被销毁时触发。
    /// </summary>
    event Action<ViewCacheItem>? ViewDestroyed;

    /// <summary>
    /// 获取或设置默认缓存过期时间（离开页面后多久销毁）。
    /// </summary>
    TimeSpan DefaultCacheExpiration { get; set; }

    /// <summary>
    /// 获取或设置清理检查的时间间隔。
    /// </summary>
    TimeSpan CleanupInterval { get; set; }

    FrameworkElement GetOrCreateView(string viewName, Func<FrameworkElement> viewFactory, string? regionName = null);

    void SetRegionName(string viewName, string regionName);

    void MarkAsActive(string viewName);

    void MarkAsDeactive(string viewName);

    void RemoveView(string viewName);

    void CleanupExpiredViews();

    void ClearCache();

    ViewCacheItem? GetCacheItem(string viewName);
}