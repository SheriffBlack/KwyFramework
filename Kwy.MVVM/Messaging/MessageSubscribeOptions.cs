namespace Kwy.MVVM.Messaging;

/// <summary>
/// 订阅消息时使用的选项。
/// </summary>
public sealed class MessageSubscribeOptions<TMessage>
{
    public static MessageSubscribeOptions<TMessage> Default { get; } = new();

    public static MessageSubscribeOptions<TMessage> OnUI { get; } = new()
    {
        Thread = MessageThread.UI
    };

    public static MessageSubscribeOptions<TMessage> OnUIReplayLatest { get; } = new()
    {
        Thread = MessageThread.UI,
        ReplayLatest = true
    };

    public static MessageSubscribeOptions<TMessage> OnBackground { get; } = new()
    {
        Thread = MessageThread.Background
    };

    /// <summary>
    /// 用于调用处理程序的线程。
    /// </summary>
    public MessageThread Thread { get; init; } = MessageThread.Publisher;

    /// <summary>
    /// 如果存在最新的保留消息，则立即回放。
    /// </summary>
    public bool ReplayLatest { get; init; }

    /// <summary>
    /// 可选过滤器。仅当过滤器返回 true 时才调用处理程序。
    /// </summary>
    public Predicate<TMessage>? Filter { get; init; }
}
