namespace Kwy.MVVM.Messaging;

/// <summary>
/// Options used when subscribing to a message.
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
    /// The thread used to invoke the handler.
    /// </summary>
    public MessageThread Thread { get; init; } = MessageThread.Publisher;

    /// <summary>
    /// Immediately replays the latest retained message if one exists.
    /// </summary>
    public bool ReplayLatest { get; init; }

    /// <summary>
    /// Optional filter. The handler is invoked only when the filter returns true.
    /// </summary>
    public Predicate<TMessage>? Filter { get; init; }
}
