namespace KwyPecvd.Device;

/// <summary>
/// PECVD功能硬件组件的非拥有型运行时索引。
/// 负责按稳定ID和能力接口查询组件；
/// 不负责创建组件、执行周期或工艺控制。
/// </summary>
public interface IHardwareComponentRegistry
{
    IReadOnlyCollection<IHardwareComponent> Components { get; }

    void Add(IHardwareComponent component);

    bool TryGet<TComponent>(
        string id,
        out TComponent component)
        where TComponent : class;

    TComponent GetRequired<TComponent>(
        string id)
        where TComponent : class;

    IReadOnlyCollection<TComponent> GetAll<TComponent>()
        where TComponent : class;
}