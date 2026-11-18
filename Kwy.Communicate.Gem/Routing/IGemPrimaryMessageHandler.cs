using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>Host Primary Message 处理 </summary>
/// <remarks>
/// 一个 Handler 通常做四件事
///     1. Parse       解析 HCMD 和参数
///     2. Validate    验证命令定义、参数和设备状态
///     3. Execute     调用设备业务处理器
///     4. Reply       构造 S2F42
/// </remarks>
public interface IGemPrimaryMessageHandler
{
    SecsMessageId MessageId { get; }

    Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default);
}
