using Kwy.UI.WPF.Converters;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// RadioButton扩展，提供 BindTo 简化 ConverterParameter提供的参数为 Content
/// </summary>
public class RadioButtonHelper
{
    /*
        | 情况                                                | 能否写回 ViewModel |
        | --------------------------------------------------- | ------------------ |
        | 给附加属性加了 BindsTwoWayByDefault                 | ✅ 能             |
        | 没加 BindsTwoWayByDefault，但 Binding.Mode=TwoWay   | ❌ 不能           |

     */

    public static readonly DependencyProperty BindToProperty = DependencyProperty.RegisterAttached(
        "BindTo",
        typeof(object),
        typeof(RadioButtonHelper),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnBindToChanged
        )
    );

    public static void SetBindTo(DependencyObject element, object value) =>
        element.SetValue(BindToProperty, value);

    public static object GetBindTo(DependencyObject element) => element.GetValue(BindToProperty);

    private static void OnBindToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadioButton radioButton)
        {
            var binding = new MultiBinding
            {
                Converter = new StringEqualityConverter(),
                ConverterParameter = radioButton.Content,
                Mode = BindingMode.TwoWay,
            };

            // ✅ 第一个绑定项：获取当前 RadioButton 的 BindTo 附加属性（即 VM 属性）
            binding.Bindings.Add(
                new Binding
                {
                    Path = new PropertyPath(BindToProperty),
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    Mode = BindingMode.TwoWay,
                }
            );

            // ✅ 第二个绑定项：RadioButton 自身的 Content
            binding.Bindings.Add(
                new Binding { Path = new PropertyPath("Content"), Source = radioButton }
            );

            // 设置 IsChecked 的多值绑定
            BindingOperations.SetBinding(radioButton, ToggleButton.IsCheckedProperty, binding);
        }
    }
}