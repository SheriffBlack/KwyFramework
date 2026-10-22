namespace Kwy.MVVM.Messaging;

/// <summary>
/// 用于跟踪消息总线的可选诊断钩子。
/// </summary>
public interface IMessageBusObserver
{
    void OnPublished(Type messageType, object? message);

    void OnHandled(Type messageType, object? recipient, object? message);

    void OnHandlerError(Type messageType, object? recipient, object? message, Exception exception);
}
