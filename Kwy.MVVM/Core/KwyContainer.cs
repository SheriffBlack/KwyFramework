namespace Kwy.MVVM.Core;

/// <summary>
/// 全局服务定位器 (核心层)
/// 允许跨平台逻辑在不引用 UI 框架的情况下访问依赖注入容器。
/// </summary>
public static class KwyContainer
{
    private static IServiceProvider? _current;

    /// <summary>
    /// 当前全局 ServiceProvider。由 KwyApplication 在初始化时注入。
    /// </summary>
    public static IServiceProvider? Current
    {
        get => _current;
        set => _current = value;
    }

    /// <summary>
    /// 根据类型解析服务
    /// </summary>
    public static T? Resolve<T>() where T : class
    {
        return Current?.GetService(typeof(T)) as T;
    }
}