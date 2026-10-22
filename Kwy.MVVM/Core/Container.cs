namespace Kwy.MVVM.Core;

/// <summary>
/// 全局服务定位器 (核心层)
/// 允许无法使用构造注入的调用方访问依赖注入容器。
/// </summary>
public static class Container
{
    private static IServiceProvider? current;

    /// <summary>
    /// 当前全局 ServiceProvider。由应用入口在初始化时注入。
    /// </summary>
    public static IServiceProvider? Current
    {
        get => current;
        set => current = value;
    }

    /// <summary>
    /// 根据类型解析服务
    /// </summary>
    public static T? Resolve<T>() where T : class
    {
        return Current?.GetService(typeof(T)) as T;
    }
}
