namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 程序启动，给IO卡信号
/// </summary>
public interface IIndustrialPcOnlineSignalMachine
{
    Task<bool> SetIndustrialPcOnlineAsync(bool online, CancellationToken cancellationToken = default);
}