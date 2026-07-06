using Kwy.MVVM.Core;
using System.Windows;

namespace Kwy.MVVM.WPF.Permissions;

/// <summary>
/// 声明式权限控制附加属性。
/// </summary>
public static class Permission
{
    public static IPermissionService? DefaultPermissionService { get; set; }

    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.RegisterAttached(
            "Code",
            typeof(string),
            typeof(Permission),
            new PropertyMetadata(null, OnPermissionChanged));

    public static readonly DependencyProperty PolicyProperty =
        DependencyProperty.RegisterAttached(
            "Policy",
            typeof(string),
            typeof(Permission),
            new PropertyMetadata(null, OnPermissionChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(PermissionCheckMode),
            typeof(Permission),
            new PropertyMetadata(PermissionCheckMode.Disable, OnPermissionChanged));

    public static readonly DependencyProperty ServiceProperty =
        DependencyProperty.RegisterAttached(
            "Service",
            typeof(IPermissionService),
            typeof(Permission),
            new PropertyMetadata(null, OnPermissionChanged));

    private static readonly DependencyProperty OriginalIsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "OriginalIsEnabled",
            typeof(bool),
            typeof(Permission),
            new PropertyMetadata(true));

    private static readonly DependencyProperty OriginalVisibilityProperty =
        DependencyProperty.RegisterAttached(
            "OriginalVisibility",
            typeof(Visibility),
            typeof(Permission),
            new PropertyMetadata(Visibility.Visible));

    private static readonly DependencyProperty SubscribedServiceProperty =
        DependencyProperty.RegisterAttached(
            "SubscribedService",
            typeof(IPermissionService),
            typeof(Permission),
            new PropertyMetadata(null));

    private static readonly DependencyProperty PermissionChangedHandlerProperty =
        DependencyProperty.RegisterAttached(
            "PermissionChangedHandler",
            typeof(EventHandler<PermissionChangedEventArgs>),
            typeof(Permission),
            new PropertyMetadata(null));

    public static string? GetCode(DependencyObject obj) => (string?)obj.GetValue(CodeProperty);

    public static void SetCode(DependencyObject obj, string? value) => obj.SetValue(CodeProperty, value);

    public static string? GetPolicy(DependencyObject obj) => (string?)obj.GetValue(PolicyProperty);

    public static void SetPolicy(DependencyObject obj, string? value) => obj.SetValue(PolicyProperty, value);

    public static PermissionCheckMode GetMode(DependencyObject obj) => (PermissionCheckMode)obj.GetValue(ModeProperty);

    public static void SetMode(DependencyObject obj, PermissionCheckMode value) => obj.SetValue(ModeProperty, value);

    public static IPermissionService? GetService(DependencyObject obj) => (IPermissionService?)obj.GetValue(ServiceProperty);

    public static void SetService(DependencyObject obj, IPermissionService? value) => obj.SetValue(ServiceProperty, value);

    private static void OnPermissionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element
            || System.ComponentModel.DesignerProperties.GetIsInDesignMode(element))
        {
            return;
        }

        if (string.IsNullOrEmpty(GetPermissionKey(element)))
        {
            UnregisterPermissionChanged(element);
            if ((e.Property == CodeProperty || e.Property == PolicyProperty)
                && !string.IsNullOrEmpty(e.OldValue as string))
            {
                RestoreOriginalState(element);
            }

            return;
        }

        if ((e.Property == CodeProperty || e.Property == PolicyProperty)
            && string.IsNullOrEmpty(e.OldValue as string))
        {
            element.SetValue(OriginalIsEnabledProperty, element.IsEnabled);
            element.SetValue(OriginalVisibilityProperty, element.Visibility);
        }

        element.Loaded -= OnElementLoaded;
        element.Loaded += OnElementLoaded;
        element.Unloaded -= OnElementUnloaded;
        element.Unloaded += OnElementUnloaded;

        RegisterPermissionChanged(element);
        RefreshPermission(element);
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            RegisterPermissionChanged(element);
            RefreshPermission(element);
        }
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UnregisterPermissionChanged(element);
        }
    }

    private static void RegisterPermissionChanged(FrameworkElement element)
    {
        UnregisterPermissionChanged(element);

        var permissionService = ResolvePermissionService(element);
        if (permissionService == null)
        {
            return;
        }

        EventHandler<PermissionChangedEventArgs> handler = (_, args) =>
        {
            string? permissionKey = GetPermissionKey(element);
            if (!string.IsNullOrEmpty(args.PermissionCode)
                && !string.Equals(args.PermissionCode, permissionKey, StringComparison.Ordinal))
            {
                return;
            }

            if (!element.Dispatcher.CheckAccess())
            {
                element.Dispatcher.InvokeAsync(() => RefreshPermission(element));
            }
            else
            {
                RefreshPermission(element);
            }
        };

        permissionService.PermissionsChanged += handler;
        element.SetValue(SubscribedServiceProperty, permissionService);
        element.SetValue(PermissionChangedHandlerProperty, handler);
    }

    private static void UnregisterPermissionChanged(FrameworkElement element)
    {
        var permissionService = (IPermissionService?)element.GetValue(SubscribedServiceProperty);
        var handler = (EventHandler<PermissionChangedEventArgs>?)element.GetValue(PermissionChangedHandlerProperty);
        if (permissionService != null && handler != null)
        {
            permissionService.PermissionsChanged -= handler;
        }

        element.ClearValue(SubscribedServiceProperty);
        element.ClearValue(PermissionChangedHandlerProperty);
    }

    private static void RefreshPermission(FrameworkElement element)
    {
        string? permissionKey = GetPermissionKey(element);
        if (string.IsNullOrEmpty(permissionKey))
        {
            RestoreOriginalState(element);
            return;
        }

        var permissionService = ResolvePermissionService(element);
        if (permissionService == null)
        {
            return;
        }

        bool hasPermission = permissionService.HasPermission(permissionKey);
        var mode = GetMode(element);

        if (!hasPermission)
        {
            RestoreOriginalState(element);

            if (mode is PermissionCheckMode.Disable or PermissionCheckMode.Both)
            {
                element.SetCurrentValue(UIElement.IsEnabledProperty, false);
            }

            if (mode is PermissionCheckMode.Hide or PermissionCheckMode.Both)
            {
                element.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            }

            return;
        }

        RestoreOriginalState(element);
    }

    private static string? GetPermissionKey(DependencyObject obj)
        => GetPolicy(obj) ?? GetCode(obj);

    private static IPermissionService? ResolvePermissionService(DependencyObject obj)
        => GetService(obj) ?? DefaultPermissionService;

    private static void RestoreOriginalState(FrameworkElement element)
    {
        element.SetCurrentValue(UIElement.IsEnabledProperty, (bool)element.GetValue(OriginalIsEnabledProperty));
        element.SetCurrentValue(UIElement.VisibilityProperty, (Visibility)element.GetValue(OriginalVisibilityProperty));
    }
}
