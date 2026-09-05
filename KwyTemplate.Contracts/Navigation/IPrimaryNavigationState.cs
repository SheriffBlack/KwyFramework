namespace KwyTemplate.Contracts.Navigation;

/// <summary>
/// 一级导航当前页面状态。
/// 供跨页面的全局输入入口判断其业务是否允许执行，避免直接依赖 MainViewModel。
/// </summary>
public interface IPrimaryNavigationState
{
    string CurrentView { get; }

    void SetCurrentView(string viewName);
}
