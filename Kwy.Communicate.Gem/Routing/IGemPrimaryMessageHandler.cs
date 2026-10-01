using Secs4Net;

namespace Kwy.Communicate.Gem;

public interface IGemPrimaryMessageHandler
{
    SecsMessageId MessageId { get; }

    Task<SecsMessage?> HandleAsync(SecsMessage message, CancellationToken cancellationToken = default);
}
