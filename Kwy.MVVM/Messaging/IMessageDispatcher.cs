namespace Kwy.MVVM.Messaging;

/// <summary>
/// UI 线程调度器抽象。
/// </summary>
public interface IMessageDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
