using Microsoft.Extensions.DependencyInjection; // 需要安装 NuGet 包
using System.ComponentModel;
using System.Windows;

namespace Kwy.MVVM.WPF.Mvvm;

/// <summary>
/// 提供基于命名约定的 ViewModel 自动装配功能。
/// 例如在 View 的 XAML 中加入 `kwy:ViewModelLocator.AutoWireViewModel="True"`，会自动去 DI 容器拉取对应的 ViewModel 并设置 DataContext。
/// 支持显式类型映射和默认命名规则。
/// </summary>
public static class ViewModelLocator
{
    private static IServiceProvider? serviceProvider;
    private static readonly AsyncLocal<IServiceProvider?> activationServiceProvider = new();

    /// <summary>
    /// 全局设置 ServiceProvider，由 KwyApplication 在启动时调用。
    /// （依赖于 DI，提供 ViewModel 实例）
    /// </summary>
    public static void SetDefaultServiceProvider(IServiceProvider serviceProvider)
    {
        ViewModelLocator.serviceProvider = serviceProvider;
    }

    internal static void ClearDefaultServiceProvider()
    {
        serviceProvider = null;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> mappings = new();

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
        mappings[viewType] = viewModelType;
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
        => AutoWire(view, activationServiceProvider.Value ?? serviceProvider);

    /// <summary>
    /// 使用指定的服务提供者为视图装配 ViewModel。
    /// 导航和对话框应传入各自的作用域 Provider，避免可释放的瞬时 ViewModel 被根容器持有。
    /// </summary>
    public static void AutoWire(DependencyObject view, IServiceProvider? serviceProvider)
    {
        if (view is FrameworkElement frameworkElement)
        {
            var viewType = view.GetType();

            // 如果已经被显式赋值了 DataContext，就不再覆盖了
            if (frameworkElement.DataContext != null)
                return;

            // 1. 先尝试从映射字典获取 (由 RegisterForNavigation 注入)
            if (!mappings.TryGetValue(viewType, out Type? viewModelType))
            {
                // 2. 如果没有显式映射，则走命名约定
                viewModelType = DefaultViewModelFactory?.Invoke(viewType);

                // 缓存命名约定的查找结果，后续可直接获取对应类型
                if (viewModelType != null)
                {
                    mappings.TryAdd(viewType, viewModelType);
                }
            }

            if (viewModelType != null && serviceProvider != null)
            {
                try
                {
                    // 先尝试直接从容器获取已注册的服务
                    object? viewModel = serviceProvider.GetService(viewModelType);

                    if (viewModel == null)
                    {
                        // 对未注册类型缓存对象工厂，避免每次创建时重复分析构造函数
                        // 对象工厂使用当前服务提供者填充构造函数依赖
                        var factory = _vmFactories.GetOrAdd(
                            viewModelType,
                            static t => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes));

                        viewModel = factory(serviceProvider, Array.Empty<object>());
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
    /// 在视图由 DI 容器构造期间设置当前作用域。
    /// 这使 XAML 加载过程中立即触发的 AutoWireViewModel 也能使用正确作用域。
    /// </summary>
    internal static IDisposable PushActivationServiceProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        var previous = activationServiceProvider.Value;
        activationServiceProvider.Value = serviceProvider;
        return new ActivationServiceProviderScope(previous);
    }

    private sealed class ActivationServiceProviderScope(IServiceProvider? previous) : IDisposable
    {
        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            activationServiceProvider.Value = previous;
        }
    }

    /// <summary>
    /// 默认的命名约定解析策略。
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

        // 如需支持 ViewModel 位于独立程序集，可扩展为遍历所有已加载程序集

        return type;
    }
}
