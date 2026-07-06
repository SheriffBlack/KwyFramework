using Kwy.MVVM.Regions;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Kwy.MVVM.WPF.Mvvm;

/// <summary>
/// 为 IServiceCollection 提供高度还原 Prism 体验的扩展方法。
/// </summary>
public static class MvvmRegistrationExtensions
{
    private static readonly List<(string Region, Type ViewType)> _deferredRegionRegistrations = new();

    /// <summary>
    /// 注册用于导航或弹窗的视图及其关联的 ViewModel。
    /// </summary>
    /// <typeparam name="TView">视图类型</typeparam>
    /// <typeparam name="TViewModel">关联的 ViewModel 类型</typeparam>
    /// <param name="services">DI 容器集合</param>
    /// <param name="regionName">可选。如果提供，将自动向该区域注册此视图 (RegisterViewWithRegion)。</param>
    /// <param name="isSingleton">是否注册为单例。默认为 false (Transient)。</param>
    /// <returns>容器集合本身，支持链式调用</returns>
    public static IServiceCollection RegisterForNavigation<TView, TViewModel>(
        this IServiceCollection services,
        string? regionName = null,
        bool isSingleton = false)
        where TView : FrameworkElement // 约束为 WPF 控件
        where TViewModel : class
    {
        // 获取 View 的名称作为 Key
        string viewName = typeof(TView).Name;

        if (isSingleton)
        {
            // 1. 注册 View 为键控单例 [核心修复]
            services.AddKeyedSingleton<FrameworkElement, TView>(viewName);
            // 2. 注册 ViewModel 为普通单例
            services.AddSingleton<TViewModel>();
        }
        else
        {
            // 1. 注册 View 为键控瞬时 [核心修复]
            services.AddKeyedTransient<FrameworkElement, TView>(viewName);
            // 2. 注册 ViewModel 为普通瞬时
            services.AddTransient<TViewModel>();
        }

        // 3. 记录 View -> ViewModel 的映射关系，供 ViewModelLocator 使用
        ViewModelLocator.Register(typeof(TView), typeof(TViewModel));

        // 4. 处理自动关联 Region 的逻辑
        if (!string.IsNullOrEmpty(regionName))
        {
            services.RegisterViewWithRegion(regionName, typeof(TView));
        }

        return services;
    }

    /// <summary>
    /// 直接将视图类型注册到特定区域。
    /// </summary>
    public static IServiceCollection RegisterViewWithRegion(this IServiceCollection services, string regionName, Type viewType)
    {
        if (Regions.RegionManager.Default != null)
        {
            Regions.RegionManager.Default.RegisterViewWithRegion(regionName, viewType);
        }
        else
        {
            // 存入延迟队列，等 RegionManager 初始化后自动清空
            _deferredRegionRegistrations.Add((regionName, viewType));
        }
        return services;
    }

    /// <summary>
    /// 内部方法：由 RegionManager 在构造阶段调用，应用之前在注册阶段堆积的导航需求。
    /// </summary>
    internal static void ApplyDeferredRegistrations(IRegionManager regionManager)
    {
        foreach (var reg in _deferredRegionRegistrations)
        {
            regionManager.RegisterViewWithRegion(reg.Region, reg.ViewType);
        }
        _deferredRegionRegistrations.Clear();
    }
}