using System.Windows;

namespace Kwy.UI.WPF.Controls;

public sealed class BindingProxy : Freezable
{
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}