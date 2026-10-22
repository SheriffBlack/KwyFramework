namespace Kwy.MVVM.Messaging;

/// <summary>
/// 没有可用的 UI 线程调度器时使用的内联调度器。
/// </summary>
public sealed class InlineMessageDispatcher : IMessageDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
