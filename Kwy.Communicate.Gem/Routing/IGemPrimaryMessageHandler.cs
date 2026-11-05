using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>Host Primary Message 处理 </summary>
public interface IGemPrimaryMessageHandler
{
    SecsMessageId MessageId { get; }

    Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default);
}
