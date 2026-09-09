using Kwy.UI.WPF.Controls;
using System.Windows;

namespace Kwy.UI.WPF.Components.Toasts;

/// <summary>
/// Connects a visual toast host to the component-level toast message service.
/// Registration is intentionally kept outside the base control library.
/// </summary>
public static class ToastHostRegistration
{
    private static readonly object SyncRoot = new();
    private static readonly List<WeakReference<KwyToastHost>> Hosts = [];

    internal static event EventHandler<KwyToastHost>? Registered;

    internal static event EventHandler<KwyToastHost>? Unregistered;

    public static readonly DependencyProperty TokenProperty = DependencyProperty.RegisterAttached(
        "Token",
        typeof(string),
        typeof(ToastHostRegistration),
        new PropertyMetadata(null, OnTokenChanged));

    public static string? GetToken(DependencyObject element)
        => (string?)element.GetValue(TokenProperty);

    public static void SetToken(DependencyObject element, string? value)
        => element.SetValue(TokenProperty, value);

    internal static IReadOnlyList<KwyToastHost> GetRegisteredHosts()
    {
        lock (SyncRoot)
        {
            PruneDeadHosts();
            return Hosts
                .Select(static reference => reference.TryGetTarget(out KwyToastHost? host) ? host : null)
                .OfType<KwyToastHost>()
                .ToArray();
        }
    }

    private static void OnTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not KwyToastHost host)
        {
            throw new InvalidOperationException("ToastHostRegistration.Token can only be set on KwyToastHost.");
        }

        host.Loaded -= OnHostLoaded;
        host.Loaded += OnHostLoaded;
        host.Unloaded -= OnHostUnloaded;
        host.Unloaded += OnHostUnloaded;

        if (host.IsLoaded)
        {
            Unregister(host);
            if (!string.IsNullOrWhiteSpace((string?)e.NewValue))
            {
                Register(host);
            }
        }
    }

    private static void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        var host = (KwyToastHost)sender;
        if (!string.IsNullOrWhiteSpace(GetToken(host)))
        {
            Register(host);
        }
    }

    private static void OnHostUnloaded(object sender, RoutedEventArgs e)
        => Unregister((KwyToastHost)sender);

    private static void Register(KwyToastHost host)
    {
        lock (SyncRoot)
        {
            PruneDeadHosts();
            if (Hosts.Any(reference => reference.TryGetTarget(out KwyToastHost? current) && ReferenceEquals(current, host)))
            {
                return;
            }

            Hosts.Add(new WeakReference<KwyToastHost>(host));
        }

        Registered?.Invoke(null, host);
    }

    private static void Unregister(KwyToastHost host)
    {
        bool removed = false;
        lock (SyncRoot)
        {
            for (int index = Hosts.Count - 1; index >= 0; index--)
            {
                if (!Hosts[index].TryGetTarget(out KwyToastHost? current) || ReferenceEquals(current, host))
                {
                    Hosts.RemoveAt(index);
                    removed = true;
                }
            }
        }

        if (removed)
        {
            Unregistered?.Invoke(null, host);
        }
    }

    private static void PruneDeadHosts()
    {
        for (int index = Hosts.Count - 1; index >= 0; index--)
        {
            if (!Hosts[index].TryGetTarget(out _))
            {
                Hosts.RemoveAt(index);
            }
        }
    }
}
