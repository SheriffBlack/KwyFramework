using Kwy.Communicate.Abstractions;
using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 管理 HSMS/SECS 通信与 Primary/Secondary 消息
/// </summary>
public interface ISecsGemClient : ICommunicationClient
{
    ConnectionState HsmsState { get; }

    Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<PrimaryMessageWrapper> GetPrimaryMessagesAsync(
        CancellationToken cancellationToken = default);
}
