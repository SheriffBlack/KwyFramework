namespace Kwy.Device.IoCard.Abstractions;

/// <summary>
/// IO 点位定义的只读查询入口。
/// 与运动模块的 <c>IAxisDefinitionProvider</c> 对应：配置层负责提供定义，业务和运行时通过稳定 ID 查询，
/// 不应各自维护 <c>Dictionary&lt;string, int&gt;</c> 等重复映射。
/// </summary>
public interface IDigitalIoPointDefinitionProvider
{
    /// <summary>设备当前加载的全部 IO 点位定义。</summary>
    IReadOnlyCollection<DigitalIoPointDefinition> Definitions { get; }

    /// <summary>按稳定逻辑 ID 获取点位定义；不存在时抛出异常。</summary>
    DigitalIoPointDefinition GetRequired(string pointId);

    /// <summary>尝试按稳定逻辑 ID 获取点位定义。</summary>
    bool TryGet(string pointId, out DigitalIoPointDefinition definition);

    /// <summary>获取指定方向的点位定义。</summary>
    IReadOnlyCollection<DigitalIoPointDefinition> GetByDirection(DigitalIoDirection direction);
}
