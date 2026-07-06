namespace Kwy.MVVM.Messaging;

/// <summary>
/// UI dispatcher abstraction. Platform projects provide concrete implementations.
/// </summary>
public interface IMessageDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
