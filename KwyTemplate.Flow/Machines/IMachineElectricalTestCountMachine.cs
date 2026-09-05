namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机型可提供 PLC 统计的电性测试合格数。
/// </summary>
public interface IMachineElectricalTestCountMachine
{
    uint ElectricalTestOkCount { get; }
}
