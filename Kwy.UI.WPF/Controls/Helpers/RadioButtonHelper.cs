using Kwy.UI.WPF.Converters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// 为 <see cref="RadioButton"/> 提供基于 <see cref="ContentControl.Content"/> 的值选择绑定。
/// </summary>
public static class RadioButtonHelper
{
    private static readonly object UnsetBindToValue = new();

    public static readonly DependencyProperty BindToProperty = DependencyProperty.RegisterAttached(
        "BindTo",
        typeof(object),
        typeof(RadioButtonHelper),
        new FrameworkPropertyMetadata(
            UnsetBindToValue,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnBindToChanged
        )
    );

    public static void SetBindTo(DependencyObject element, object? value) =>
        element.SetValue(BindToProperty, value);

    public static object? GetBindTo(DependencyObject element)
    {
        object value = element.GetValue(BindToProperty);
        return ReferenceEquals(value, UnsetBindToValue) ? null : value;
    }

    private static void OnBindToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RadioButton radioButton)
        {
            return;
        }

        EnsureCheckedHandler(radioButton);
        var binding = new MultiBinding
        {
            Converter = new EqualityConverter(),
            Mode = BindingMode.OneWay
        };
        binding.Bindings.Add(new Binding
        {
            Path = new PropertyPath(BindToProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.Self),
            Mode = BindingMode.OneWay
        });
        binding.Bindings.Add(new Binding
        {
            Path = new PropertyPath(nameof(ContentControl.Content)),
            RelativeSource = new RelativeSource(RelativeSourceMode.Self),
            Mode = BindingMode.OneWay
        });
        BindingOperations.SetBinding(radioButton, ToggleButton.IsCheckedProperty, binding);
    }

    private static readonly DependencyProperty IsCheckedHandlerAttachedProperty =
        DependencyProperty.RegisterAttached(
            "IsCheckedHandlerAttached",
            typeof(bool),
            typeof(RadioButtonHelper),
            new PropertyMetadata(false));

    private static void EnsureCheckedHandler(RadioButton radioButton)
    {
        if ((bool)radioButton.GetValue(IsCheckedHandlerAttachedProperty))
        {
            return;
        }

        radioButton.Checked += OnRadioButtonChecked;
        radioButton.SetValue(IsCheckedHandlerAttachedProperty, true);
    }

    private static void OnRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton
            && !Equals(GetBindTo(radioButton), radioButton.Content))
        {
            SetBindTo(radioButton, radioButton.Content);
        }
    }
}
