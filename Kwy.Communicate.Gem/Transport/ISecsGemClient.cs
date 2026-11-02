using Kwy.Communicate.Abstractions;
using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 管理 HSMS/SECS 通信与 Primary/Secondary 消息。
/// </summary>
public interface ISecsGemClient : IProtocolClient
{
    ConnectionState HsmsState { get; }

    Task<SecsMessage?> SendAsync(SecsMessage message, CancellationToken cancellationToken = default);

    /// <summary>持续读取对端主动发起的 Primary Message，直到取消或消息源结束。</summary>
    /// <remarks>本端通过 <see cref="SendAsync"/> 发送 Primary 后收到的 Secondary Reply 由该方法直接返回，不会出现在此消息流中。</remarks>
    IAsyncEnumerable<PrimaryMessageWrapper> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
