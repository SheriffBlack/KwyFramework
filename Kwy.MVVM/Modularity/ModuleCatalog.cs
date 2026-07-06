namespace Kwy.MVVM.Modularity;

/// <summary>
/// 基础的模型目录实现。允许硬编码式（手动）将模块类加入登记表中。
/// 未来你也可以继承此类（通过反射如 DirectoryModuleCatalog）来做到自动扫描特定文件夹下的所有 .dll 并自动填入！
/// </summary>
public class ModuleCatalog : IModuleCatalog
{
    private readonly List<Type> _modules = new();

    public IEnumerable<Type> Modules => _modules;

    public IModuleCatalog AddModule(Type moduleType)
    {
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

    public IModuleCatalog AddModule<TModule>() where TModule : IModule
    {
        return AddModule(typeof(TModule));
    }
}