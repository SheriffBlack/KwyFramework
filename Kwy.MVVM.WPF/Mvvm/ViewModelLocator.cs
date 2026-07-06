using Microsoft.Extensions.DependencyInjection; // 需要安装 NuGet 包
using System.ComponentModel;
using System.Windows;

namespace Kwy.MVVM.WPF.Mvvm;

/// <summary>
/// 提供基于约定（Convention）的 ViewModel 自动装配功能。
/// 例如在 View 的 XAML 中加入 `kwy:ViewModelLocator.AutoWireViewModel="True"`，会自动去 DI 容器拉取对应的 ViewModel 并设置 DataContext。
/// 相当于 Prism.Mvvm.ViewModelLocator。
/// </summary>
public static class ViewModelLocator
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// 全局设置 ServiceProvider，由 KwyApplication 在启动时调用。
    /// （依赖于 DI，提供 ViewModel 实例）
    /// </summary>
    public static void SetDefaultServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    internal static void ClearDefaultServiceProvider()
    {
        _serviceProvider = null;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> _mappings = new();

    /// <summary>
    /// 指示是否自动装配 ViewModel 的附加属性。
    /// 设置为 True 时进行自动装配。
    /// </summary>
    public static readonly DependencyProperty AutoWireViewModelProperty =
        DependencyProperty.RegisterAttached(
            "AutoWireViewModel",
            typeof(bool?),
            typeof(ViewModelLocator),
            new PropertyMetadata(null, OnAutoWireViewModelChanged));

    public static bool? GetAutoWireViewModel(DependencyObject obj)
    {
        return (bool?)obj.GetValue(AutoWireViewModelProperty);
    }

    public static void SetAutoWireViewModel(DependencyObject obj, bool? value)
    {
        obj.SetValue(AutoWireViewModelProperty, value);
    }

    private static void OnAutoWireViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(d))
            return;

        if (e.NewValue is bool b && b)
        {
            AutoWire(d);
        }
    }

    /// <summary>
    /// 手动映射 View 和 ViewModel 的类型，优先级高于命名约定。
    /// </summary>
    public static void Register(Type viewType, Type viewModelType)
    {
        _mappings[viewType] = viewModelType;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, ObjectFactory> _vmFactories = new();

    /// <summary>
    /// 全局委托：决定给定一个 View 类型，它对应的 ViewModel 类型是什么。
    /// </summary>
    public static Func<Type, Type?>? DefaultViewModelFactory { get; set; } = DefaultViewModelTypeResolver;

    /// <summary>
    /// 核心装配逻辑：根据约定为 View 寻找并实例化 ViewModel，并设置为其 DataContext。
    /// </summary>
    public static void AutoWire(DependencyObject view)
    {
        if (view is FrameworkElement frameworkElement)
        {
            var viewType = view.GetType();

            // 如果已经被显式赋值了 DataContext，就不再覆盖了
            if (frameworkElement.DataContext != null)
                return;

            // 1. 先尝试从映射字典获取 (由 RegisterForNavigation 注入)
            if (!_mappings.TryGetValue(viewType, out Type? viewModelType))
            {
                // 2. 如果没有显式映射，则走命名约定
                viewModelType = DefaultViewModelFactory?.Invoke(viewType);

                // 【性能压榨】：将约定查找的结果缓存下来！下次再解析这个 View 时，O(1) 极速直达！
                if (viewModelType != null)
                {
                    _mappings.TryAdd(viewType, viewModelType);
                }
            }

            if (viewModelType != null && _serviceProvider != null)
            {
                try
                {
                    // 先尝试直接从容器获取已注册的服务
                    object? viewModel = _serviceProvider.GetService(viewModelType);

                    if (viewModel == null)
                    {
                        // 【极致压栈性能优化】：不要每次使用反射的 CreateInstance！
                        // 使用 ActivatorUtilities.CreateFactory 编译动态方法并缓存委托，从而在未注册服务中直接享受零反射实例化！
                        var factory = _vmFactories.GetOrAdd(
                            viewModelType,
                            static t => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes));

                        viewModel = factory(_serviceProvider, Array.Empty<object>());
                    }

                    frameworkElement.DataContext = viewModel;
                }
                catch (Exception ex)
                {
                    string message = $"[ViewModelLocator] 无法为视图 '{viewType.FullName}' 创建对应的 ViewModel '{viewModelType.FullName}'。请检查该 ViewModel 是否已注册及其构造函数参数是否都在 DI 容器中。";
                    throw new InvalidOperationException(message, ex);
                }
            }
        }
    }

    /// <summary>
    /// 最基础的基于命名约定的解析策略（同 Prism 默认行为相似）。
    /// 将形如: `ProjectName.Views.MainView` 或者 `ProjectName.Views.Folder.MainView`
    /// 映射到: `ProjectName.ViewModels.MainViewModel` 或者 `ProjectName.ViewModels.Folder.MainViewModel`
    /// </summary>
    private static Type? DefaultViewModelTypeResolver(Type viewType)
    {
        string? viewName = viewType.FullName;
        if (string.IsNullOrEmpty(viewName))
            return null;

        // 核心替换 1: 将 ".Views." 名称空间替换为 ".ViewModels."
        string viewModelName = viewName.Replace(".Views.", ".ViewModels.");

        // 核心替换 2: 如果当前视图是以 "View" 结尾（如 MainView），替换/增加为 "ViewModel"
        if (viewModelName.EndsWith("View"))
        {
            viewModelName += "Model";
        }
        else if (viewModelName.EndsWith("Window"))
        {
            viewModelName += "ViewModel";
        }
        else
        {
            viewModelName += "ViewModel";
        }

        // 尝试从包含该 View 的同个程序集中去拉取对应的类型
        Type? type = viewType.Assembly.GetType(viewModelName);

        // 若找不到可以扩展：遍历所有已加载的 Assembly，以应对 ViewModel 放在单独程序集的情况。

        return type;
    }
}
