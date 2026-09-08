using Kwy.UI.Threading;
using System.Windows.Threading;

namespace Kwy.UI.WPF.Threading;

/// <summary>
/// WPF adapter for scheduling presentation updates on a dispatcher.
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher dispatcher;
    private readonly DispatcherPriority priority;

    public WpfUiDispatcher(Dispatcher dispatcher, DispatcherPriority priority = DispatcherPriority.DataBind)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.priority = priority;
    }

    public bool CheckAccess() => dispatcher.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(priority, action);
    }
}
