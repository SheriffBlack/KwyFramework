namespace Kwy.MVVM.Modularity;

/// <summary>
/// 管理模块初始化生命周期。按需模块的服务会在容器构建前注册，
/// 但只有调用 LoadModule 时才执行 OnInitialized。
/// </summary>
public interface IModuleManager
{
    bool IsModuleLoaded(string moduleName);

    void LoadModule(string moduleName);

    void LoadModule<TModule>() where TModule : IModule;
}
