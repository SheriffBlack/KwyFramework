using System.Windows;
using System.Windows.Input;

namespace Kwy.UI.WPF.Controls.Helpers;

public static class InputAdornerHelper
{
    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.RegisterAttached("Unit", typeof(string), typeof(InputAdornerHelper), new PropertyMetadata(string.Empty));

    public static string GetUnit(DependencyObject obj) => (string)obj.GetValue(UnitProperty);
    public static void SetUnit(DependencyObject obj, string value) => obj.SetValue(UnitProperty, value);

    public static readonly DependencyProperty ButtonCommandProperty =
        DependencyProperty.RegisterAttached("ButtonCommand", typeof(ICommand), typeof(InputAdornerHelper), new PropertyMetadata(null));

    public static ICommand? GetButtonCommand(DependencyObject obj) => (ICommand?)obj.GetValue(ButtonCommandProperty);
    public static void SetButtonCommand(DependencyObject obj, ICommand? value) => obj.SetValue(ButtonCommandProperty, value);

    public static readonly DependencyProperty ButtonContentProperty =
        DependencyProperty.RegisterAttached("ButtonContent", typeof(object), typeof(InputAdornerHelper), new PropertyMetadata(null));

    public static object? GetButtonContent(DependencyObject obj) => obj.GetValue(ButtonContentProperty);
    public static void SetButtonContent(DependencyObject obj, object? value) => obj.SetValue(ButtonContentProperty, value);

    public static readonly DependencyProperty ButtonCommandParameterProperty =
        DependencyProperty.RegisterAttached("ButtonCommandParameter", typeof(object), typeof(InputAdornerHelper), new PropertyMetadata(null));

    public static object? GetButtonCommandParameter(DependencyObject obj) => obj.GetValue(ButtonCommandParameterProperty);
    public static void SetButtonCommandParameter(DependencyObject obj, object? value) => obj.SetValue(ButtonCommandParameterProperty, value);
}
