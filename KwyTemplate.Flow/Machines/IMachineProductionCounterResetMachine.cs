namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机型支持将 PLC 内的生产统计计数清零。
/// </summary>
public interface IMachineProductionCounterResetMachine
{
    /// <summary>
    /// 对机型定义的统计计数清零点位写入 1。
    /// </summary>
    Task<bool> ResetProductionCounterAsync(CancellationToken cancellationToken = default);
}