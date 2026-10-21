namespace Kwy.Device.IoCard.Abstractions;

/// <summary>模拟量 IO 点位定义的只读查询入口。</summary>
public interface IAnalogIoPointDefinitionProvider
{
    IReadOnlyCollection<AnalogIoPointDefinition> Definitions { get; }
    AnalogIoPointDefinition GetRequired(string pointId);
    bool TryGet(string pointId, out AnalogIoPointDefinition definition);
    IReadOnlyCollection<AnalogIoPointDefinition> GetByDirection(AnalogIoDirection direction);
}
