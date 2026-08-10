using Kwy.MVVM.Core;
using Kwy.MVVM.WPF.Mvvm;
using Kwy.MVVM.WPF.Permissions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Windows;

namespace Kwy.MVVM.WPF;

/// <summary>
/// 基础 WPF 应用程序类，提供依赖注入和模块化支持。
/// 相当于 Prism.Unity 里的 PrismApplication 基类。
/// </summary>
public abstract class KwyApplication : Application
{
    private static readonly TimeSpan ServiceProviderDisposeTimeout = TimeSpan.FromSeconds(4);
    /// <summary>
    /// 获取全局的依赖注入服务提供者 (IServiceProvider)。
    /// </summary>
    public static IServiceProvider? CurrentServiceProvider { get; private set; }

    /// <summary>
    /// 在应用程序启动时重写，用于初始化 DI 容器、注册服务、集成模块化以及启动主窗口。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. 创建 ServiceCollection
        var services = new ServiceCollection();

        // 2. 注册内部框架级别服务 (例如 RegionManager, DialogService 等)
        RegisterRequiredTypes(services);

        // 3. 供子类使用，注册用户自定义的服务和 ViewModel
        RegisterTypes(services);

        // 4. 初始化模块化系统：构建目录并实例化所有已配置的模块
        var moduleCatalog = CreateModuleCatalog();

        // 【新增】：如果是目录扫描器，必须先触发 Initialize 扫描本地文件，把 Type 装进池子
        if (moduleCatalog is Kwy.MVVM.Modularity.DirectoryModuleCatalog dirCatalog)
        {
            dirCatalog.Initialize();
        }

        // 这里依然保留 ConfigureModuleCatalog，因为你可能还会硬编码加入一些“内置”的核心模块
        ConfigureModuleCatalog(moduleCatalog);
        services.AddSingleton<Kwy.MVVM.Modularity.IModuleCatalog>(moduleCatalog);

        // 【核心修复】：对模块进行拓扑排序，确保被依赖的模块先加载！
        var sortedModuleTypes = SortModulesByDependencies(moduleCatalog.Modules);

        var moduleInstances = new List<Modularity.IModule>();

        foreach (var moduleType in sortedModuleTypes)
        {
            if (Activator.CreateInstance(moduleType) is Modularity.IModule module)
            {
                moduleInstances.Add(module);
                module.RegisterTypes(services);
                services.AddSingleton(typeof(Modularity.IModule), module);
                services.AddSingleton(moduleType, module);
            }
        }

        services.AddSingleton<Modularity.IModuleManager, Modularity.ModuleManager>();

        // 5. 构建全局唯一的 ServiceProvider
        CurrentServiceProvider = services.BuildServiceProvider();

        // 6. 将 IServiceProvider 传递给全局定位器支持跨平台访问
        Core.KwyContainer.Current = CurrentServiceProvider;
        ViewModelLocator.SetDefaultServiceProvider(CurrentServiceProvider);
        Permission.DefaultPermissionService = ResolvePermissionService(CurrentServiceProvider);

        // 确保 RegionManager 在 Shell 的 Region 控件加载前完成初始化。
        _ = CurrentServiceProvider.GetRequiredService<MVVM.Regions.IRegionManager>();

        // 容器已经可用，但模块还没有开始初始化。启动页等早期 UI 可以在这里显示。
        OnServiceProviderCreated(CurrentServiceProvider);

        // 7. 初始化非按需模块。按需模块由 IModuleManager.LoadModule 显式激活。
        var moduleManager = CurrentServiceProvider.GetRequiredService<Modularity.IModuleManager>();
        foreach (var moduleType in sortedModuleTypes.Where(type => !Modularity.ModuleManager.IsOnDemand(type)))
        {
            moduleManager.LoadModule(Modularity.ModuleManager.GetModuleName(moduleType));
        }

        // 8. 创建主窗体 (Shell)
        var shell = CreateShell();
        if (shell != null)
        {
            // 9. 初始化主窗体壳子 (钩子)
            InitializeShell(shell);

            if (shell is Window window)
            {
                MainWindow = window;
            }

            // 10. 显示主窗体
            shell.Show();
        }

        // 11. 整个生命周期加载完成 (钩子)
        OnInitialized();
    }

    /// <summary>
    /// 基于 Kahn 算法的拓扑排序，解决模块间的依赖加载顺序问题。
    /// </summary>
    private IEnumerable<Type> SortModulesByDependencies(IEnumerable<Type> modules)
    {
        var moduleList = modules.ToList();
        var moduleDictionary = new Dictionary<string, Type>();
        var dependencies = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        // 1. 构建模块字典和初始化入度
        foreach (var type in moduleList)
        {
            // 获取模块名称 (优先读取特性，否则用类名)
            var moduleName = Modularity.ModuleManager.GetModuleName(type);

            if (!moduleDictionary.TryAdd(moduleName, type))
            {
                throw new InvalidOperationException($"检测到重复的模块名称 '{moduleName}'。");
            }

            dependencies[moduleName] = new List<string>();
            inDegree[moduleName] = 0;
        }

        // 2. 解析依赖关系图
        foreach (var type in moduleList)
        {
            var moduleName = Modularity.ModuleManager.GetModuleName(type);

            var depAttrs = type.GetCustomAttributes<Kwy.MVVM.Modularity.ModuleDependencyAttribute>();
            foreach (var depAttr in depAttrs)
            {
                var depName = depAttr.ModuleName;
                if (!moduleDictionary.ContainsKey(depName))
                {
                    throw new InvalidOperationException($"模块 '{moduleName}' 依赖于未找到的模块 '{depName}'。");
                }

                // 记录依赖：被依赖者 -> 依赖者
                dependencies[depName].Add(moduleName);
                inDegree[moduleName]++;
            }
        }

        // 3. 执行 Kahn 拓扑排序算法
        var queue = new Queue<string>();
        foreach (var kvp in inDegree.Where(kvp => kvp.Value == 0))
        {
            queue.Enqueue(kvp.Key);
        }

        var sortedNames = new List<string>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sortedNames.Add(current);

            foreach (var dependent in dependencies[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // 4. 循环依赖检测
        if (sortedNames.Count != moduleList.Count)
        {
            throw new InvalidOperationException("检测到模块间存在循环依赖，应用程序无法启动！");
        }

        // 5. 返回排序后的类型列表
        return sortedNames.Select(name => moduleDictionary[name]);
    }

    /// <summary>
    /// 创建并返回模块目录。默认返回一个基础的硬编码 ModuleCatalog。
    /// （如果想做类似动态 DLL 扫描的功能，可重写此法返回自定义的目录模型）。
    /// </summary>
    protected virtual Modularity.IModuleCatalog CreateModuleCatalog()
    {
        return new Modularity.ModuleCatalog();
    }

    /// <summary>
    /// 配置模块目录。子类可重载以在此处手动使用 AddModule() 登记各类需加载的外部模块。
    /// 比如：moduleCatalog.AddModule<SettingsModule>();
    /// </summary>
    protected virtual void ConfigureModuleCatalog(Modularity.IModuleCatalog moduleCatalog)
    {
    }

    /// <summary>
    /// 注册框架必需的服务（子类如果需要覆盖，请务必调用 base.RegisterRequiredTypes）。
    /// 这里未来将注册 IRegionManager, IDialogService 等组件。
    /// </summary>
    protected virtual void RegisterRequiredTypes(IServiceCollection services)
    {
        // 注册全局缓存管理器
        services.AddSingleton<Regions.IViewCacheManager, Regions.ViewCacheManager>();

        // 注册全局 RegionManager
        services.AddSingleton<MVVM.Regions.IRegionManager, Regions.RegionManager>();

        // 注册全局 DialogService
        services.AddSingleton<MVVM.Dialogs.IDialogService, Dialogs.DialogService>();
        services.AddTransient<Dialogs.IDialogWindow, Dialogs.DefaultDialogWindow>();

        // 注册全局消息总线 MessageBus。
        services.AddSingleton<Kwy.MVVM.Messaging.IMessageDispatcher>(_ =>
            new Kwy.MVVM.WPF.Messaging.WpfMessageDispatcher(Application.Current.Dispatcher));
        Kwy.MVVM.Messaging.ServiceCollectionExtensions.AddKwyMessageBus(services);

        services.AddSingleton<IAuthorizationService>(provider =>
        {
            var permissionService = ResolvePermissionService(provider)
                ?? throw new InvalidOperationException(
                    $"使用 {nameof(IAuthorizationService)} 前，请先注册 {nameof(IPermissionService)}。");
            return new PermissionAuthorizationService(permissionService);
        });
    }

    /// <summary>
    /// 派生类必须实现此方法以注册自定义的服务与 ViewModel（注入 IoC 容器）。
    /// </summary>
    /// <param name="services">DI 容器收集器</param>
    protected abstract void RegisterTypes(IServiceCollection services);

    /// <summary>
    /// ServiceProvider 创建完成后的回调。此时模块尚未初始化，适合显示启动页或读取启动期共享状态。
    /// </summary>
    protected virtual void OnServiceProviderCreated(IServiceProvider serviceProvider)
    { }

    /// <summary>
    /// 派生类必须实现此方法以创建应用程序的主窗体 (Shell)。
    /// </summary>
    protected abstract Window CreateShell();

    /// <summary>
    /// 初始化主窗体。此时窗体已创建但尚未显示。
    /// </summary>
    protected virtual void InitializeShell(Window shell)
    { }

    /// <summary>
    /// 应用程序完全初始化完成（包括模块加载和窗体显示）后的回调。
    /// </summary>
    protected virtual void OnInitialized()
    { }

    protected override void OnExit(ExitEventArgs e)
    {
        ViewModelLocator.ClearDefaultServiceProvider();
        Core.KwyContainer.Current = null;
        Permission.DefaultPermissionService = null;

        IServiceProvider? provider = CurrentServiceProvider;
        CurrentServiceProvider = null;
        DisposeServiceProviderWithTimeout(provider);
        base.OnExit(e);
    }

    private static void DisposeServiceProviderWithTimeout(IServiceProvider? provider)
    {
        if (provider == null)
        {
            return;
        }

        try
        {
            Task disposeTask = Task.Run(async () =>
            {
                if (provider is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().AsTask().ConfigureAwait(false);
                    return;
                }

                if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            });

            _ = disposeTask.Wait(ServiceProviderDisposeTimeout);
        }
        catch
        {
        }
    }

    private static IPermissionService? ResolvePermissionService(IServiceProvider serviceProvider)
        => serviceProvider.GetService<IPermissionService>();
}

