using System.Windows;
using System.Windows.Input;

namespace Kwy.UI.WPF.Controls.Helpers;

public static class MouseHelper
{
    #region DoubleClickCommand
    public static readonly DependencyProperty DoubleClickCommandProperty =
        DependencyProperty.RegisterAttached(
            "DoubleClickCommand",
            typeof(ICommand),
            typeof(MouseHelper),
            new PropertyMetadata(null, OnDoubleClickCommandChanged));

    public static void SetDoubleClickCommand(DependencyObject element, ICommand? value)
        => element.SetValue(DoubleClickCommandProperty, value);

    public static ICommand? GetDoubleClickCommand(DependencyObject element)
        => (ICommand?)element.GetValue(DoubleClickCommandProperty);
    #endregion

    #region DoubleClickCommandParameter
    public static readonly DependencyProperty DoubleClickCommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "DoubleClickCommandParameter",
            typeof(object),
            typeof(MouseHelper),
            new PropertyMetadata(null));

    public static void SetDoubleClickCommandParameter(DependencyObject element, object? value)
        => element.SetValue(DoubleClickCommandParameterProperty, value);

    public static object? GetDoubleClickCommandParameter(DependencyObject element)
        => element.GetValue(DoubleClickCommandParameterProperty);
    #endregion

    private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            // 改成 PreviewMouseLeftButtonDown
            element.PreviewMouseLeftButtonDown -= Element_PreviewMouseLeftButtonDown;
            if (e.NewValue is ICommand)
            {
                element.PreviewMouseLeftButtonDown += Element_PreviewMouseLeftButtonDown;
            }
        }
    }

    private static void Element_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is UIElement element)
        {
            var command = GetDoubleClickCommand(element);
            var parameter = GetDoubleClickCommandParameter(element);

            if (command != null && command.CanExecute(parameter))
            {
                command.Execute(parameter);
                e.Handled = true; // 拦截双击，防止它继续往下传变成普通单击
            }
        }
    }

}
