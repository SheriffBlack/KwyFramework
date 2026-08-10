namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机型支持在生产结束时输出 MES 汇总文件。
/// </summary>
public interface IMachineProductionSummaryMachine
{
    Task SaveProductionSummaryAsync(CancellationToken cancellationToken = default);
}
