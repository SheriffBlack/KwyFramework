using System.Windows.Threading;
using Kwy.MVVM.Messaging;

namespace Kwy.MVVM.WPF.Messaging;

public sealed class WpfMessageDispatcher : IMessageDispatcher
{
    private readonly Dispatcher dispatcher;

    public WpfMessageDispatcher(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public bool CheckAccess() => dispatcher.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        dispatcher.BeginInvoke(action);
    }
}
