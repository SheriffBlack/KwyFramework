namespace Kwy.MVVM.Regions;

/// <summary>
/// 区域管理器接口。
/// 提供向 UI 占位符 (Region) 中注册视图、导航的功能。
/// 放在核心库 (Kwy.MVVM) 中，使得 ViewModel 不必直接引用 WPF 即可要求页面导航。
/// </summary>
public interface IRegionManager
{
    /// <summary>
    /// 当导航过程发生异常时触发。
    /// 统一 API 风格：让调用者决定如何处理错误信息。
    /// </summary>
    event Action<NavigationResult>? NavigationFailed;

    /// <summary>
    /// 【全局监控】：当应用中任何一个 Region 成功完成一次导航时触发。
    /// 可以用来代替 Prism 的 RegionBehavior 做全局路由日志、面包屑导航或埋点。
    /// </summary>
    event Action<NavigationContext>? Navigated;

    /// <summary>
    /// 将指定的视图类型注册到指定名称的区域中。
    /// 该操作无需等待控件加载，区域控件一旦出现在 UI 中，管理器将自动实例化并填入该视图。
    /// 相当于 Prism 的 `_regionManager.RegisterViewWithRegion("MainRegion", typeof(ViewA));`
    /// </summary>
    void RegisterViewWithRegion(string regionName, Type viewType);

    /// <summary>
    /// 【现代化】向指定的区域请求导航，并支持 await 异步等待导航生命周期完成。
    /// </summary>
    /// <param name="regionName">区域名称</param>
    /// <param name="target">目标视图的注册名</param>
    /// <param name="parameters">导航参数（可选）</param>
    /// <returns>包含导航结果与上下文的 Task</returns>
    Task<NavigationResult> RequestNavigateAsync(string regionName, string target, INavigationParameters? parameters = null);

    /// <summary>
    /// 向指定的区域请求导航到指定名称的视图。
    /// </summary>
    void RequestNavigate(string regionName, string viewName);

    /// <summary>
    /// 向指定的区域请求导航到指定类型的视图。
    /// </summary>
    void RequestNavigate(string regionName, Type viewType);

    /// <summary>
    /// 向指定的区域请求导航，并支持参数传递与结果回调。
    /// </summary>
    void RequestNavigate(string regionName, string target, Action<NavigationResult> navigationCallback, INavigationParameters parameters);

    /// <summary>
    /// 向指定的区域请求导航，并支持参数传递。
    /// </summary>
    void RequestNavigate(string regionName, string target, INavigationParameters parameters);

    /// <summary>
    /// 向指定的区域请求导航，并支持结果回调。
    /// </summary>
    void RequestNavigate(string regionName, string target, Action<NavigationResult> navigationCallback);

    /// <summary>
    /// 检查指定名称的区域是否存在。
    /// </summary>
    bool ContainsRegion(string regionName);

    /// <summary>
    /// 获取指定区域当前正在显示的活动视图实例。
    /// </summary>
    object? GetActiveView(string regionName);
}