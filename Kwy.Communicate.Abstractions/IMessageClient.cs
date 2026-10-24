namespace Kwy.Communicate.Abstractions;

using Kwy.Communicate.Abstractions.Events;

/// <summary>
/// 面向消息的通信客户端。
/// </summary>
public interface IMessageClient<TMessage> : IProtocolClient
{
    /// <summary>接收到消息时发布。</summary>
    event EventHandler<MessageReceivedEventArgs<TMessage>>? MessageReceived;

    /// <summary>发送一条消息。</summary>
    ValueTask SendAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>持续读取所有接收到的消息，直到取消或消息源结束。</summary>
    IAsyncEnumerable<TMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}
