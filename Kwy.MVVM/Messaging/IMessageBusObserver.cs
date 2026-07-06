namespace Kwy.MVVM.Messaging;

/// <summary>
/// Optional diagnostics hook for message bus tracing.
/// </summary>
public interface IMessageBusObserver
{
    void OnPublished(Type messageType, object? message);

    void OnHandled(Type messageType, object? recipient, object? message);

    void OnHandlerError(Type messageType, object? recipient, object? message, Exception exception);
}
