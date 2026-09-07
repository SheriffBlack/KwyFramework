using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机型支持把编带参数写入 PLC。App 层只提交通用编带参数，具体 PLC 点位由机型内部决定。
/// </summary>
public interface IMachineBraidSetupMachine
{
    Task ApplyBraidSetupAsync(MesWorkOrderTapeSetup tapeSetup, CancellationToken cancellationToken = default);
}
