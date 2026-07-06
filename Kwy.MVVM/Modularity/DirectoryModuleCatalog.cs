using System.Runtime.Loader;

namespace Kwy.MVVM.Modularity;

/// <summary>
/// 基于 .NET 8 AssemblyLoadContext 的目录模块扫描器。
/// 专为解决工业视觉平台动态加载算法 DLL 插件而生。
/// </summary>
public class DirectoryModuleCatalog : IModuleCatalog
{
    public const string DefaultModulePath = "Modules";

    private readonly List<Type> _modules = new();

    public IEnumerable<Type> Modules => _modules;

    /// <summary>
    /// 插件存放的相对或绝对路径 (默认值为 "Modules" 或 "Plugins")
    /// </summary>
    public string ModulePath { get; set; } = DefaultModulePath;

    public IModuleCatalog AddModule(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException($"类型 {moduleType.Name} 必须实现 {nameof(IModule)} 接口。", nameof(moduleType));
        }

        if (!_modules.Contains(moduleType))
        {
            _modules.Add(moduleType);
        }
        return this;
    }

    public IModuleCatalog AddModule<TModule>() where TModule : IModule => AddModule(typeof(TModule));

    /// <summary>
    /// 执行扫描的核心方法。在应用程序启动前期被调用。
    /// </summary>
    public void Initialize()
    {
        if (string.IsNullOrWhiteSpace(ModulePath)) return;

        var fullPath = Path.GetFullPath(ModulePath);
        if (!Directory.Exists(fullPath))
        {
            // 如果文件夹不存在，可以自动创建一个，方便实施人员丢 DLL
            Directory.CreateDirectory(fullPath);
            return;
        }

        // 查找目录下所有的 .dll 文件
        var dllFiles = Directory.GetFiles(fullPath, "*.dll");

        foreach (var file in dllFiles)
        {
            try
            {
                // 【核心科技】使用 .NET 8 的 AssemblyLoadContext 进行安全加载
                // Default 上下文适合不需要热卸载的常规插件
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);

                // 扫描该 DLL 中所有实现了 IModule 接口的非抽象类
                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

                foreach (var type in moduleTypes)
                {
                    AddModule(type);
                }
            }
            catch (BadImageFormatException)
            {
                // 忽略非 C# 的纯 C++ 算法库 (比如直接丢进来的 opencv_world.dll)
            }
            catch (Exception ex)
            {
                // TODO: 记录日志，提示某个插件加载失败 (依赖缺失等)
                System.Diagnostics.Debug.WriteLine($"加载插件失败 {file}: {ex.Message}");
            }
        }
    }
}
