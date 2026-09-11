using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Hosts lightweight toast messages.
/// </summary>
public class KwyToastHost : ItemsControl
{
    private readonly Dictionary<KwyToast, CancellationTokenSource> pendingRemovals = new();

    public KwyToastHost()
    {
        DefaultStyleKey = typeof(KwyToastHost);
        Unloaded += OnHostUnloaded;
    }

    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(KwyToastHost), new PropertyMetadata(TimeSpan.FromSeconds(3)),
            static value => value is TimeSpan duration && duration >= TimeSpan.Zero);

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public static readonly DependencyProperty MaxItemsProperty =
        DependencyProperty.Register(nameof(MaxItems), typeof(int), typeof(KwyToastHost), new PropertyMetadata(5),
            static value => value is int count && count >= 0);

    public KwyToastPlacement Placement
    {
        get => (KwyToastPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(KwyToastPlacement), typeof(KwyToastHost), new PropertyMetadata(KwyToastPlacement.Top),
            static value => value is KwyToastPlacement placement && Enum.IsDefined(placement));

    protected override AutomationPeer OnCreateAutomationPeer()
        => new KwyToastHostAutomationPeer(this);

    public void Show(object message, object? icon = null, Brush? accentBrush = null, TimeSpan? duration = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Show(message, icon, accentBrush, duration)));
            return;
        }

        var toast = new KwyToast
        {
            Content = message,
            Icon = icon
        };

        if (accentBrush != null)
        {
            toast.Foreground = accentBrush;
            toast.BorderBrush = accentBrush;
        }

        Items.Add(toast);
        TrimOverflow();
        ScheduleRemoval(toast, duration ?? Duration);
    }

    public void Clear()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Clear));
            return;
        }

        CancelPendingRemovals();
        Items.Clear();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is KwyToast;

    protected override DependencyObject GetContainerForItemOverride()
        => new KwyToast();

    private void TrimOverflow()
    {
        if (MaxItems <= 0)
        {
            return;
        }

        while (Items.Count > MaxItems)
        {
            if (Items[0] is KwyToast toast)
            {
                Remove(toast);
            }
            else
            {
                Items.RemoveAt(0);
            }
        }
    }

    private void ScheduleRemoval(KwyToast toast, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        pendingRemovals[toast] = cancellation;
        _ = RemoveAfterDelayAsync(toast, duration, cancellation.Token);
    }

    private async Task RemoveAfterDelayAsync(KwyToast toast, TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => Remove(toast), System.Windows.Threading.DispatcherPriority.Normal, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The toast was cleared or the host left the visual tree.
        }
    }

    private void Remove(KwyToast toast)
    {
        if (Items.Contains(toast))
        {
            Items.Remove(toast);
        }

        if (pendingRemovals.Remove(toast, out CancellationTokenSource? cancellation))
        {
            cancellation.Dispose();
        }
    }

    private void OnHostUnloaded(object sender, RoutedEventArgs e)
        => CancelPendingRemovals();

    private void CancelPendingRemovals()
    {
        foreach (CancellationTokenSource cancellation in pendingRemovals.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        pendingRemovals.Clear();
    }
}
