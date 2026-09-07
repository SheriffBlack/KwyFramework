using System.Threading;
using System.Threading.Tasks;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 支持按工单首次启动时向 PLC 输出“新工单”信号的机型能力。
/// </summary>
public interface IMachineWorkOrderStartSignalMachine
{
    /// <summary>
    /// 设置当前准备启动的工单号。
    /// 同一个工单重复设置不重新计为新工单；工单号变化后，下一次启动会重新输出新工单信号。
    /// </summary>
    void SetCurrentWorkOrder(string? workOrderNo);

    /// <summary>
    /// 复位工单启动相关输出信号，通常在程序退出或机台释放时调用。
    /// </summary>
    Task ResetWorkOrderStartSignalsAsync(CancellationToken cancellationToken = default);
}
