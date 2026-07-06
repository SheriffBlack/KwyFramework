using Kwy.UI.WPF.Controls;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace Kwy.UI.WPF.Components.Toasts;

internal sealed class ToastMessageService : IToastMessageService, IDisposable
{
    private readonly ConcurrentDictionary<string, WeakReference<KwyToastHost>> hosts =
        new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    public ToastMessageService()
    {
        KwyToastHost.Registered += OnHostRegistered;
        KwyToastHost.Unregistered += OnHostUnregistered;

        foreach (var host in KwyToastHost.GetRegisteredHosts())
        {
            RegisterHost(host);
        }
    }

    public void Show(string message, ToastMessageOptions? options = null)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        options ??= new ToastMessageOptions();
        if (!TryGetHost(options.Token, out var host))
        {
            return;
        }

        host.Show(message, ResolveIcon(options.Icon), ResolveBrush(options.Icon), options.Duration);
    }

    public void Show(string message, DialogMessageIcon icon, TimeSpan? duration = null, string? token = null)
        => Show(
            message,
            new ToastMessageOptions
            {
                Token = NormalizeToken(token),
                Icon = icon,
                Duration = duration
            });

    public void ShowSuccess(string message, TimeSpan? duration = null, string? token = null)
        => Show(message, DialogMessageIcon.Success, duration, token);

    public void ShowInfo(string message, TimeSpan? duration = null, string? token = null)
        => Show(message, DialogMessageIcon.Info, duration, token);

    public void ShowWarning(string message, TimeSpan? duration = null, string? token = null)
        => Show(message, DialogMessageIcon.Warning, duration, token);

    public void ShowError(string message, TimeSpan? duration = null, string? token = null)
        => Show(message, DialogMessageIcon.Error, duration, token);

    public void Clear(string? token = null)
    {
        ThrowIfDisposed();
        if (TryGetHost(NormalizeToken(token), out var host))
        {
            host.Clear();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        KwyToastHost.Registered -= OnHostRegistered;
        KwyToastHost.Unregistered -= OnHostUnregistered;
        hosts.Clear();
    }

    private void OnHostRegistered(object? sender, KwyToastHost host)
        => RegisterHost(host);

    private void OnHostUnregistered(object? sender, KwyToastHost host)
    {
        string token = NormalizeToken(host.Token);
        if (!hosts.TryGetValue(token, out var reference))
        {
            return;
        }

        if (!reference.TryGetTarget(out var current) || ReferenceEquals(current, host))
        {
            hosts.TryRemove(token, out _);
        }
    }

    private bool TryGetHost(string? token, out KwyToastHost host)
    {
        string actualToken = NormalizeToken(token);
        if (hosts.TryGetValue(actualToken, out var reference) &&
            reference.TryGetTarget(out host!))
        {
            return true;
        }

        hosts.TryRemove(actualToken, out _);
        host = null!;
        return false;
    }

    private static string NormalizeToken(string? token)
        => string.IsNullOrWhiteSpace(token) ? ToastTokens.Root : token;

    private void RegisterHost(KwyToastHost host)
        => hosts[NormalizeToken(host.Token)] = new WeakReference<KwyToastHost>(host);

    private static object? ResolveIcon(DialogMessageIcon icon)
    {
        string? resourceKey = icon switch
        {
            DialogMessageIcon.Success => "IconSuccess",
            DialogMessageIcon.Info => "IconInfo",
            DialogMessageIcon.Error => "IconError",
            DialogMessageIcon.Question => "IconQuestion",
            DialogMessageIcon.Warning => "IconWarning",
            _ => null
        };

        return resourceKey == null ? null : Application.Current?.TryFindResource(resourceKey);
    }

    private static Brush? ResolveBrush(DialogMessageIcon icon)
    {
        string? resourceKey = icon switch
        {
            DialogMessageIcon.Success => "SuccessBrush",
            DialogMessageIcon.Info or DialogMessageIcon.Question => "InfoBrush",
            DialogMessageIcon.Error => "ErrorBrush",
            DialogMessageIcon.Warning => "WarningBrush",
            _ => null
        };

        return resourceKey == null ? null : Application.Current?.TryFindResource(resourceKey) as Brush;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ToastMessageService));
        }
    }
}
