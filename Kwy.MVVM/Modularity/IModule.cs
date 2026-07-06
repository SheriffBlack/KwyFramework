using Microsoft.Extensions.DependencyInjection;

namespace Kwy.MVVM.Modularity;

/// <summary>
/// 模块化接口。每个独立拆分的业务项目需实现此接口，以便在应用程序启动时无缝集成。
/// 完全对应 Prism 的 IModule 概念（分离注册阶段与初始化阶段）。
/// </summary>
public interface IModule
{
    /// <summary>
    /// 第一阶段：在 DI 容器生成之前被调用。
    /// 模块应在此处将自己持有的业务服务(Services)、视图或特有的组件注册进全局的 DI 容器 (services)。
    /// </summary>
    void RegisterTypes(IServiceCollection services);

    /// <summary>
    /// 第二阶段：在全局 DI 容器构建完成后被调用。
    /// 模块应在此处进行初始化逻辑，例如通过 IRegionManager 向 Region 注入自己的主视图，或订阅全局的事件通信。
    /// </summary>
    void OnInitialized(IServiceProvider provider);
}