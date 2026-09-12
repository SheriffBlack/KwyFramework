using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kwy.UI.WPF.Controls;

namespace Kwy.UI.WPF.Input.Keyboard;

/// <summary>
/// Attaches the application-local soft keyboard to supported text input controls.
/// A dispatcher owns at most one active keyboard session.
/// </summary>
public static class SoftKeyboardService
{
    private static readonly ConditionalWeakTable<Dispatcher, SoftKeyboardCoordinator> Coordinators = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SoftKeyboardService), new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.RegisterAttached(
        "Layout", typeof(KeyboardLayout), typeof(SoftKeyboardService), new PropertyMetadata(KeyboardLayout.Qwerty),
        static value => value is KeyboardLayout layout && Enum.IsDefined(layout));

    public static void SetLayout(DependencyObject element, KeyboardLayout value) => element.SetValue(LayoutProperty, value);
    public static KeyboardLayout GetLayout(DependencyObject element) => (KeyboardLayout)element.GetValue(LayoutProperty);

    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode", typeof(KwyKeyboardMode), typeof(SoftKeyboardService), new PropertyMetadata(KwyKeyboardMode.Full),
        static value => value is KwyKeyboardMode mode && Enum.IsDefined(mode));

    public static void SetMode(DependencyObject element, KwyKeyboardMode value) => element.SetValue(ModeProperty, value);
    public static KwyKeyboardMode GetMode(DependencyObject element) => (KwyKeyboardMode)element.GetValue(ModeProperty);

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative", typeof(bool), typeof(SoftKeyboardService), new PropertyMetadata(true));

    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);

    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.RegisterAttached(
        "DecimalPlaces", typeof(int?), typeof(SoftKeyboardService), new PropertyMetadata(null),
        static value => value is null || (value is int places && places is >= 0 and <= 15));

    public static void SetDecimalPlaces(DependencyObject element, int? value) => element.SetValue(DecimalPlacesProperty, value);
    public static int? GetDecimalPlaces(DependencyObject element) => (int?)element.GetValue(DecimalPlacesProperty);

    public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached(
        "Width", typeof(double), typeof(SoftKeyboardService), new PropertyMetadata(760d),
        static value => value is double width && double.IsFinite(width) && width > 0);

    public static void SetWidth(DependencyObject element, double value) => element.SetValue(WidthProperty, value);
    public static double GetWidth(DependencyObject element) => (double)element.GetValue(WidthProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || d is not (TextBox or KwyNumberBox))
        {
            throw new InvalidOperationException("SoftKeyboardService can only be attached to a TextBox or KwyNumberBox.");
        }

        Detach(element);
        if ((bool)e.NewValue)
        {
            GetCoordinator(element.Dispatcher).WarmUp(element);
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            element.PreviewTouchDown += OnPreviewTouchDown;
            element.Unloaded += OnElementUnloaded;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryRequestOpen(sender))
        {
            e.Handled = true;
        }
    }

    private static void OnPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        if (TryRequestOpen(sender))
        {
            // Prevent WPF from promoting the same touch to a second mouse request.
            e.Handled = true;
        }
    }

    private static bool TryRequestOpen(object? sender)
        => sender is FrameworkElement element
           && GetIsEnabled(element)
           && GetCoordinator(element.Dispatcher).RequestOpen(element);

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element
            && Coordinators.TryGetValue(element.Dispatcher, out SoftKeyboardCoordinator? coordinator))
        {
            coordinator.CloseIfOwnedBy(element);
        }
    }

    private static void Detach(FrameworkElement element)
    {
        element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        element.PreviewTouchDown -= OnPreviewTouchDown;
        element.Unloaded -= OnElementUnloaded;
        if (Coordinators.TryGetValue(element.Dispatcher, out SoftKeyboardCoordinator? coordinator))
        {
            coordinator.CloseIfOwnedBy(element);
        }
    }

    private static SoftKeyboardCoordinator GetCoordinator(Dispatcher dispatcher)
        => Coordinators.GetValue(dispatcher, static owner => new SoftKeyboardCoordinator(owner));
}
