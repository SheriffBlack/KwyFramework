using Kwy.MVVM.Regions;
using Kwy.MVVM.WPF.Mvvm;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.MVVM.WPF.Regions;

/// <summary>
/// 优化版区域管理器：基于 .NET 8 键控服务 (Keyed Services) 实现。
/// 彻底抛弃 AppDomain 遍历，实现零反射导航。
/// </summary>
public class RegionManager : IRegionManager, IDisposable
{
    // 全局静态实例，用于 XAML 附加属性快速定位容器
    public static RegionManager? Default { get; internal set; }

    #region XAML 附加属性 (RegionName)

    public static readonly DependencyProperty RegionNameProperty =
        DependencyProperty.RegisterAttached(
            "RegionName",
            typeof(string),
            typeof(RegionManager),
            new PropertyMetadata(null, OnRegionNameChanged));

    public static string? GetRegionName(DependencyObject obj) => (string?)obj.GetValue(RegionNameProperty);

    public static void SetRegionName(DependencyObject obj, string? value) => obj.SetValue(RegionNameProperty, value);

    private static void OnRegionNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ContentControl control) return;
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(d)) return;

        control.Loaded -= OnRegionControlLoaded;
        control.Unloaded -= OnRegionControlUnloaded;

        if (e.OldValue is string oldRegionName && !string.IsNullOrEmpty(oldRegionName))
        {
            Default?.RemoveActiveRegion(oldRegionName, control);
        }

        if (e.NewValue is not string newRegionName || string.IsNullOrEmpty(newRegionName))
        {
            return;
        }

        control.Loaded += OnRegionControlLoaded;
        control.Unloaded += OnRegionControlUnloaded;

        if (control.IsLoaded)
        {
            Default?.AddActiveRegion(newRegionName, control);
        }
    }

    private static void OnRegionControlLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl control && Default != null)
        {
            var regionName = GetRegionName(control);
            if (!string.IsNullOrEmpty(regionName))
                Default.AddActiveRegion(regionName, control);
        }
    }

    private static void OnRegionControlUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl control && Default != null)
        {
            var regionName = GetRegionName(control);
            if (!string.IsNullOrEmpty(regionName))
                Default.RemoveActiveRegion(regionName, control);
        }
    }

    #endregion XAML 附加属性 (RegionName)

    private readonly IServiceProvider _serviceProvider;

    // 活跃区域字典：RegionName -> ContentControl (UI 挂载点)
    private readonly ConcurrentDictionary<string, ContentControl> _activeRegions = new ConcurrentDictionary<string, ContentControl>();

    // 默认视图注册：Region 控件加载后自动导航。
    private readonly ConcurrentDictionary<string, string> _defaultViews = new();

    // 视图名称只能归属于一个 Region，避免同一实例跨视觉树挂载。
    private readonly ConcurrentDictionary<string, string> _viewOwners = new();

    // 每个 Region 独立串行导航，不阻塞其他 Region。
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _navigationLocks = new();

    // 延迟导航队列：当 Region 控件尚未加载完成时，暂存导航请求
    private readonly ConcurrentDictionary<string, (string Target, INavigationParameters? Params, Action<NavigationResult>? Callback)> _deferredRequests = new();

    public event Action<NavigationResult>? NavigationFailed;

    public event Action<NavigationContext>? Navigated;

    public RegionManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Default = this;

        // 【核心补丁】：把之前在服务注册阶段堆积的“默认关联”应用掉
        MvvmRegistrationExtensions.ApplyDeferredRegistrations(this);

        // 默认行为：仅在调试模式下打印，不依赖外部日志库
#if DEBUG
        NavigationFailed += (result) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Kwy.MVVM.RegionManager] 导航失败: {result.Error?.Message}");
        };
#endif
    }

    #region 内部生命周期管理 (由附加属性调用)

    internal void AddActiveRegion(string regionName, ContentControl control)
    {
        _activeRegions[regionName] = control;

        // 检查是否有由于 UI 未就绪而积压的导航请求，并在就绪后立即触发
        if (_deferredRequests.TryRemove(regionName, out var request))
        {
            NavigateAndInvokeCallback(regionName, request.Target, request.Params, request.Callback);
        }
        else if (_defaultViews.TryGetValue(regionName, out var defaultView))
        {
            NavigateAndInvokeCallback(regionName, defaultView, null, null);
        }
    }

    internal void RemoveActiveRegion(string regionName, ContentControl control)
    {
        if (_activeRegions.TryGetValue(regionName, out var activeControl)
            && ReferenceEquals(activeControl, control))
        {
            _activeRegions.TryRemove(regionName, out _);
        }
    }

    #endregion 内部生命周期管理 (由附加属性调用)

    #region IRegionManager 异步核心引擎

    /// <summary>
    /// 【核心优化】支持 await 的异步导航引擎。
    /// 解决了 WPF 视觉树 Logical Child 冲突与非确定性内存清理。
    /// </summary>
    public async Task<NavigationResult> RequestNavigateAsync(string regionName, string target, INavigationParameters? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        EnsureViewOwnership(regionName, target);

        var context = new NavigationContext(regionName, target, parameters ?? new NavigationParameters());

        // 1. 查找目标 UI 容器
        if (!_activeRegions.TryGetValue(regionName, out var regionControl))
        {
            // 若容器未就绪，将最新请求放入延迟队列，等待 Region 加载。
            _deferredRequests[regionName] = (target, parameters, null);
            return new NavigationResult(false, null, context);
        }

        var navigationLock = _navigationLocks.GetOrAdd(regionName, static _ => new SemaphoreSlim(1, 1));
        await navigationLock.WaitAsync();
        try
        {
            if (!_activeRegions.TryGetValue(regionName, out regionControl))
            {
                _deferredRequests[regionName] = (target, parameters, null);
                return new NavigationResult(false, null, context);
            }

            var cacheManager = _serviceProvider.GetService<IViewCacheManager>();
            var oldView = regionControl.Content as FrameworkElement;
            var oldViewModel = oldView?.DataContext;

            // 2. 视图复用判断：如果当前显示的正是目标视图，且 ViewModel 允许处理该导航，则直接返回
            if (oldView != null && oldView.GetType().Name == target && oldViewModel != null)
            {
                if (IsNavigationTarget(oldViewModel, context))
                {
                    await OnNavigatedToAsync(oldViewModel, context);

                    // 【全局路由事件】：触发导航完成通知
                    Navigated?.Invoke(context);

                    return new NavigationResult(true) { Context = context };
                }
            }

            // 3. 触发旧视图离场逻辑 (OnNavigatedFrom)
            if (oldViewModel != null)
            {
                await OnNavigatedFromAsync(oldViewModel, context);
            }

            // 【极致性能关键 1】: 在解析新视图前，先切断旧视图的视觉树连接。
            // 这样做可以防止缓存复用时 WPF 抛出 "已经属于另一个父级" 的致命异常。
            regionControl.Content = null;

            // 4. 通知缓存管理器将旧视图标记为闲置 (Cached)
            if (cacheManager != null && oldView != null)
            {
                cacheManager.MarkAsDeactive(oldView.GetType().Name);
            }

            // 5. 【极致性能关键 2】: 使用 .NET 8 键控服务进行 O(1) 解析。
            // 相比原来的 FindTypeByFullName 全局扫包，这里不再需要反射，直接由容器返回实例。
            FrameworkElement newView;
            if (cacheManager != null)
            {
                newView = cacheManager.GetOrCreateView(target, () =>
                    _serviceProvider.GetRequiredKeyedService<FrameworkElement>(target), regionName);
            }
            else
            {
                newView = _serviceProvider.GetRequiredKeyedService<FrameworkElement>(target);
            }

            // 自动装配 ViewModel (如果 DataContext 为空且没有明确禁止)
            if (newView.DataContext == null)
            {
                var autoWire = ViewModelLocator.GetAutoWireViewModel(newView);
                if (autoWire == null || (autoWire is bool b && b))
                {
                    ViewModelLocator.AutoWire(newView);
                }
            }

            // 6. 挂载新视图到视觉树
            regionControl.Content = newView;

            // 7. 触发新视图入场逻辑 (OnNavigatedTo)
            if (newView.DataContext is object newViewModel)
            {
                await OnNavigatedToAsync(newViewModel, context);
            }

            // 8. 【全局路由事件】：触发导航完成通知
            Navigated?.Invoke(context);

            return new NavigationResult(true) { Context = context };
        }
        catch (Exception ex)
        {
            var failureResult = NavigationResult.Failure(ex, context);

            // 统一 API 风格：触发事件，而不是硬编码打印日志
            NavigationFailed?.Invoke(failureResult);

            return failureResult;
        }
        finally
        {
            navigationLock.Release();
        }
    }

    #endregion IRegionManager 异步核心引擎

    #region 兼容性与辅助方法

    public void RequestNavigate(string regionName, string target)
        => RequestNavigate(regionName, target, null, null);

    public void RequestNavigate(string regionName, string target, Action<NavigationResult>? callback, INavigationParameters? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        EnsureViewOwnership(regionName, target);

        if (!_activeRegions.ContainsKey(regionName))
        {
            _deferredRequests[regionName] = (target, parameters, callback);
            return;
        }

        NavigateAndInvokeCallback(regionName, target, parameters, callback);
    }

    public void RegisterViewWithRegion(string regionName, Type viewType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);
        ArgumentNullException.ThrowIfNull(viewType);

        EnsureViewOwnership(regionName, viewType.Name);
        _defaultViews[regionName] = viewType.Name;
        if (ContainsRegion(regionName))
        {
            RequestNavigate(regionName, viewType.Name);
        }
    }

    public bool ContainsRegion(string regionName) => _activeRegions.ContainsKey(regionName);

    public object? GetActiveView(string regionName)
        => _activeRegions.TryGetValue(regionName, out var c) ? c.Content : null;

    public void RequestNavigate(string regionName, Type viewType) => RequestNavigate(regionName, viewType.Name);

    public void RequestNavigate(string regionName, string target, Action<NavigationResult> navigationCallback)
        => RequestNavigate(regionName, target, navigationCallback, null);

    public void RequestNavigate(string regionName, string target, INavigationParameters parameters)
        => RequestNavigate(regionName, target, null, parameters);

    private async void NavigateAndInvokeCallback(
        string regionName,
        string target,
        INavigationParameters? parameters,
        Action<NavigationResult>? callback)
    {
        var result = await RequestNavigateAsync(regionName, target, parameters);
        callback?.Invoke(result);
    }

    private void EnsureViewOwnership(string regionName, string target)
    {
        string owner = _viewOwners.GetOrAdd(target, regionName);
        if (!string.Equals(owner, regionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"视图 '{target}' 已归属于 Region '{owner}'，不能同时用于 Region '{regionName}'。");
        }
    }

    private static bool IsNavigationTarget(object viewModel, NavigationContext context)
        => viewModel switch
        {
            IAsyncNavigationAware asyncAware => asyncAware.IsNavigationTarget(context),
            INavigationAware aware => aware.IsNavigationTarget(context),
            _ => false
        };

    private static Task OnNavigatedToAsync(object viewModel, NavigationContext context)
        => viewModel switch
        {
            IAsyncNavigationAware asyncAware => asyncAware.OnNavigatedToAsync(context),
            INavigationAware aware => InvokeSynchronous(() => aware.OnNavigatedTo(context)),
            _ => Task.CompletedTask
        };

    private static Task OnNavigatedFromAsync(object viewModel, NavigationContext context)
        => viewModel switch
        {
            IAsyncNavigationAware asyncAware => asyncAware.OnNavigatedFromAsync(context),
            INavigationAware aware => InvokeSynchronous(() => aware.OnNavigatedFrom(context)),
            _ => Task.CompletedTask
        };

    private static Task InvokeSynchronous(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _activeRegions.Clear();
        _defaultViews.Clear();
        _viewOwners.Clear();
        _deferredRequests.Clear();

        foreach (var navigationLock in _navigationLocks.Values)
        {
            navigationLock.Dispose();
        }
        _navigationLocks.Clear();

        if (ReferenceEquals(Default, this))
        {
            Default = null;
        }

        GC.SuppressFinalize(this);
    }

    #endregion 兼容性与辅助方法
}
