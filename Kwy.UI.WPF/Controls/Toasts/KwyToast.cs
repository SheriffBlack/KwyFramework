using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Lightweight non-blocking message item.
/// </summary>
public class KwyToast : ContentControl
{
    public KwyToast()
    {
        DefaultStyleKey = typeof(KwyToast);
    }

    [Bindable(true)]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(object), typeof(KwyToast), new PropertyMetadata(null));

    [Bindable(true)]
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(KwyToast), new PropertyMetadata(18d),
            static value => value is double size && double.IsFinite(size) && size >= 0);

    [Bindable(true)]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(KwyToast), new PropertyMetadata(new CornerRadius(4)));

    protected override AutomationPeer OnCreateAutomationPeer()
        => new KwyToastAutomationPeer(this);
}
