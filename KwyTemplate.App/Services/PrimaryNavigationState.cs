using KwyTemplate.Contracts.Navigation;

namespace KwyTemplate.App.Services;

/// <summary>
/// 一级导航状态的应用级单一来源。
/// </summary>
public sealed class PrimaryNavigationState : IPrimaryNavigationState
{
    public string CurrentView { get; private set; } = string.Empty;

    public void SetCurrentView(string viewName)
        => CurrentView = viewName ?? string.Empty;
}
