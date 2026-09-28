using Kwy.Communicate.Abstractions;
using Secs4Net;

namespace Kwy.Communicate.Gem;

public interface ISecsGemClient : ICommunicationClient
{
    ConnectionState HsmsState { get; }

    Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<PrimaryMessageWrapper> GetPrimaryMessagesAsync(
        CancellationToken cancellationToken = default);
}
