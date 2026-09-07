namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机型可提供首页展示的 PLC 生产计数。
/// </summary>
public interface IMachineProductionCountMachine
{
    uint MaterialInputCount { get; }

    uint ElectricalTestOkCount { get; }
}
