namespace Kwy.MVVM.Messaging;

/// <summary>
/// Dispatcher used when no UI platform dispatcher is available.
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
