using System.Reflection;

namespace Kwy.MVVM.Modularity;

/// <summary>
/// 默认模块生命周期管理器。
/// </summary>
public sealed class ModuleManager : IModuleManager
{
    private readonly IServiceProvider serviceProvider;
    private readonly Dictionary<string, ModuleEntry> modules;
    private readonly HashSet<string> loadedModules = new(StringComparer.Ordinal);
    private readonly object syncRoot = new();

    public ModuleManager(IServiceProvider serviceProvider, IEnumerable<IModule> moduleInstances)
    {
        this.serviceProvider = serviceProvider;
        modules = moduleInstances
            .Select(module => new ModuleEntry(GetModuleName(module.GetType()), module, GetDependencies(module.GetType())))
            .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    }

    public bool IsModuleLoaded(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        lock (syncRoot)
        {
            return loadedModules.Contains(moduleName);
        }
    }

    public void LoadModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        lock (syncRoot)
        {
            LoadModuleCore(moduleName);
        }
    }

    public void LoadModule<TModule>() where TModule : IModule
        => LoadModule(GetModuleName(typeof(TModule)));

    private void LoadModuleCore(string moduleName)
    {
        if (loadedModules.Contains(moduleName))
        {
            return;
        }

        if (!modules.TryGetValue(moduleName, out var entry))
        {
            throw new InvalidOperationException($"未找到名为 '{moduleName}' 的模块。");
        }

        foreach (string dependency in entry.Dependencies)
        {
            LoadModuleCore(dependency);
        }

        entry.Instance.OnInitialized(serviceProvider);
        loadedModules.Add(moduleName);
    }

    public static string GetModuleName(Type moduleType)
    {
        var attribute = moduleType.GetCustomAttribute<ModuleAttribute>();
        return string.IsNullOrWhiteSpace(attribute?.ModuleName) ? moduleType.Name : attribute.ModuleName;
    }

    public static bool IsOnDemand(Type moduleType)
        => moduleType.GetCustomAttribute<ModuleAttribute>()?.OnDemand == true;

    private static IReadOnlyList<string> GetDependencies(Type moduleType)
        => moduleType
            .GetCustomAttributes<ModuleDependencyAttribute>()
            .Select(attribute => attribute.ModuleName)
            .ToArray();

    private sealed record ModuleEntry(string Name, IModule Instance, IReadOnlyList<string> Dependencies);
}
