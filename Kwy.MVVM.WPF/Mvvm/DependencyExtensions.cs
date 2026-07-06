using Microsoft.Extensions.DependencyInjection;

namespace Kwy.MVVM.WPF.Mvvm;

/// <summary>
/// 为 Microsoft.Extensions.DependencyInjection 提供的增强扩展。
/// 提供类似 Prism 的透明解析 (Resolve) 能力。
/// </summary>
public static class DependencyExtensions
{
    /// <summary>
    /// 解析指定类型的实例。
    /// 即使该类型没有在容器中注册，只要其构造函数依赖项已满足，亦可自动创建实例（透明实例化）。
    /// </summary>
    public static T Resolve<T>(this IServiceProvider provider)
    {
        return (T)Resolve(provider, typeof(T));
    }

    /// <summary>
    /// 解析指定类型的实体。
    /// </summary>
    public static object Resolve(this IServiceProvider provider, Type type)
    {
        // 1. 先尝试从容器直接拿（如果已注册）
        var instance = provider.GetService(type);
        if (instance != null) return instance;

        // 2. 如果没注册，利用 ActivatorUtilities 强行解析（前提是依赖项都在容器里）
        try
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(provider, type);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"[KwyContainer] 无法解析类型 '{type.Name}'。请检查其构造函数中的依赖项是否已全部注册。\n错误详情: {ex.Message}", ex);
        }
    }
}