using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// 在宿主窗口完成默认焦点处理后，为控件设置首次焦点。
/// </summary>
public static class FocusHelper
{
    public static readonly DependencyProperty IsInitialFocusProperty =
        DependencyProperty.RegisterAttached(
            "IsInitialFocus",
            typeof(bool),
            typeof(FocusHelper),
            new PropertyMetadata(false, OnIsInitialFocusChanged));

    public static void SetIsInitialFocus(DependencyObject element, bool value)
        => element.SetValue(IsInitialFocusProperty, value);

    public static bool GetIsInitialFocus(DependencyObject element)
        => (bool)element.GetValue(IsInitialFocusProperty);

    private static void OnIsInitialFocusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        if (!(bool)e.NewValue)
        {
            return;
        }

        if (element.IsLoaded)
        {
            RequestFocus(element);
            return;
        }

        element.Loaded += OnElementLoaded;
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        RequestFocus(element);
    }

    private static void RequestFocus(FrameworkElement element)
    {
        element.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (!GetIsInitialFocus(element) || !element.IsVisible || !element.IsEnabled)
            {
                return;
            }

            Keyboard.Focus(element);
            if (element is TextBox textBox)
            {
                textBox.SelectAll();
            }
        });
    }
}
