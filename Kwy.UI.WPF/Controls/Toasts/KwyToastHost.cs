using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Hosts lightweight toast messages.
/// </summary>
public class KwyToastHost : ItemsControl
{
    private static readonly object SyncRoot = new();
    private static readonly List<WeakReference<KwyToastHost>> Hosts = new();

    public static event EventHandler<KwyToastHost>? Registered;

    public static event EventHandler<KwyToastHost>? Unregistered;

    public KwyToastHost()
    {
        DefaultStyleKey = typeof(KwyToastHost);
        Unloaded += OnHostUnloaded;
    }

    public string? Token
    {
        get => (string?)GetValue(TokenProperty);
        set => SetValue(TokenProperty, value);
    }

    public static readonly DependencyProperty TokenProperty =
        DependencyProperty.Register(nameof(Token), typeof(string), typeof(KwyToastHost), new PropertyMetadata("RootToast"));

    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(KwyToastHost), new PropertyMetadata(TimeSpan.FromSeconds(3)));

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public static readonly DependencyProperty MaxItemsProperty =
        DependencyProperty.Register(nameof(MaxItems), typeof(int), typeof(KwyToastHost), new PropertyMetadata(5));

    public KwyToastPlacement Placement
    {
        get => (KwyToastPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(KwyToastPlacement), typeof(KwyToastHost), new PropertyMetadata(KwyToastPlacement.Top));

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
        _ = RemoveAfterDelayAsync(toast, duration ?? Duration);
    }

    public void Clear()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Clear));
            return;
        }

        Items.Clear();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is KwyToast;

    protected override DependencyObject GetContainerForItemOverride()
        => new KwyToast();

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        AddRegisteredHost(this);
        Registered?.Invoke(this, this);
    }

    public static IReadOnlyList<KwyToastHost> GetRegisteredHosts()
    {
        lock (SyncRoot)
        {
            var hosts = new List<KwyToastHost>(Hosts.Count);
            for (int i = Hosts.Count - 1; i >= 0; i--)
            {
                if (Hosts[i].TryGetTarget(out var host))
                {
                    hosts.Add(host);
                }
                else
                {
                    Hosts.RemoveAt(i);
                }
            }

            return hosts;
        }
    }

    private void TrimOverflow()
    {
        if (MaxItems <= 0)
        {
            return;
        }

        while (Items.Count > MaxItems)
        {
            Items.RemoveAt(0);
        }
    }

    private async Task RemoveAfterDelayAsync(KwyToast toast, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(duration).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() => Remove(toast));
    }

    private void Remove(KwyToast toast)
    {
        if (Items.Contains(toast))
        {
            Items.Remove(toast);
        }
    }

    private void OnHostUnloaded(object sender, RoutedEventArgs e)
    {
        RemoveRegisteredHost(this);
        Unregistered?.Invoke(this, this);
    }

    private static void AddRegisteredHost(KwyToastHost host)
    {
        lock (SyncRoot)
        {
            for (int i = Hosts.Count - 1; i >= 0; i--)
            {
                if (!Hosts[i].TryGetTarget(out var current))
                {
                    Hosts.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(current, host))
                {
                    return;
                }
            }

            Hosts.Add(new WeakReference<KwyToastHost>(host));
        }
    }

    private static void RemoveRegisteredHost(KwyToastHost host)
    {
        lock (SyncRoot)
        {
            for (int i = Hosts.Count - 1; i >= 0; i--)
            {
                if (!Hosts[i].TryGetTarget(out var current) || ReferenceEquals(current, host))
                {
                    Hosts.RemoveAt(i);
                }
            }
        }
    }
}
