namespace Kwy.MVVM.Modularity;

/// <summary>
/// 模块目录接口。管理并存放系统中所有必须载入的模块。
/// </summary>
public interface IModuleCatalog
{
    /// <summary>
    /// 获取目录中所有已声明的模块类别信息。
    /// </summary>
    IEnumerable<Type> Modules { get; }

    /// <summary>
    /// 向目录中注册一个新的模块类型。
    /// </summary>
    IModuleCatalog AddModule(Type moduleType);

    /// <summary>
    /// 泛型辅助方法：向目录中注册一个新的模块类型。
    /// </summary>
    IModuleCatalog AddModule<TModule>() where TModule : IModule;
}