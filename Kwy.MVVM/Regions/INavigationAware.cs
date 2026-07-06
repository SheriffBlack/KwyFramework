namespace Kwy.MVVM.Regions;

/// <summary>
/// 导航上下文，包含导航目标、参数等信息。
/// </summary>
public class NavigationContext
{
    /// <summary>
    /// 导航目标的标识。
    /// </summary>
    public string Uri { get; }

    /// <summary>
    /// 触发此次导航的区域名称。
    /// 【新增】：为了让全局路由记录（如日志记录器）知道是哪个模块发生了界面切换。
    /// </summary>
    public string RegionName { get; }

    /// <summary>
    /// 伴随导航传递的参数。
    /// </summary>
    public INavigationParameters Parameters { get; }

    public NavigationContext(string regionName, string uri, INavigationParameters parameters)
    {
        RegionName = regionName;
        Uri = uri;
        Parameters = parameters ?? new NavigationParameters();
    }
}

/// <summary>
/// 导航感知接口。
/// 当 ViewModel 实现此接口时，RegionManager 在导航过程中会触发相应的生命周期方法。
/// 对应 Prism 的 INavigationAware。
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// 当导航到此视图时触发。
    /// </summary>
    void OnNavigatedTo(NavigationContext navigationContext);

    /// <summary>
    /// 是否允许此视图处理该导航请求（通常用于 View 复用判断）。
    /// </summary>
    bool IsNavigationTarget(NavigationContext navigationContext);

    /// <summary>
    /// 当从当前视图导航开时触发。
    /// </summary>
    void OnNavigatedFrom(NavigationContext navigationContext);
}

/// <summary>
/// 支持异步导航生命周期的能力接口。
/// RegionManager 会等待当前生命周期完成后，再处理同一 Region 的下一次导航。
/// </summary>
public interface IAsyncNavigationAware
{
    Task OnNavigatedToAsync(NavigationContext navigationContext);

    bool IsNavigationTarget(NavigationContext navigationContext);

    Task OnNavigatedFromAsync(NavigationContext navigationContext);
}
