namespace Kwy.MVVM.Messaging;

/// <summary>
/// Weak-reference application message bus.
/// </summary>
public interface IMessageBus
{
    void Publish<TMessage>(TMessage message)
        where TMessage : class;

    void Publish<TMessage>(TMessage message, MessagePublishOptions options)
        where TMessage : class;

    ValueTask PublishAsync<TMessage>(
        TMessage message,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    IDisposable Subscribe<TMessage>(
        object recipient,
        Action<TMessage> handler)
        where TMessage : class;

    IDisposable Subscribe<TRecipient, TMessage>(
        TRecipient recipient,
        Action<TRecipient, TMessage> handler)
        where TRecipient : class
        where TMessage : class;

    IDisposable Subscribe<TMessage>(
        object recipient,
        Action<TMessage> handler,
        MessageSubscribeOptions<TMessage> options)
        where TMessage : class;

    IDisposable Subscribe<TRecipient, TMessage>(
        TRecipient recipient,
        Action<TRecipient, TMessage> handler,
        MessageSubscribeOptions<TMessage> options)
        where TRecipient : class
        where TMessage : class;

    void Unsubscribe<TMessage>(object recipient)
        where TMessage : class;

    void Unsubscribe(object recipient);

    bool TryGetLatest<TMessage>(out TMessage? message)
        where TMessage : class;

    void ClearLatest<TMessage>()
        where TMessage : class;
}
