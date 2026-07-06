using Kwy.UI.WPF.Behaviors;
using Kwy.UI.WPF.Converters;
using Microsoft.Xaml.Behaviors;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/*
 <TextBlock
    Text="{Binding TotalCount}"
    local:NumberFormatHelper.Mode="Culture" />

 <TextBox
    Text="{Binding TotalCount}"
    local:NumberFormatHelper.Mode="Culture" />

<TextBlock
    Text="{Binding TotalCount}"
    local:NumberFormatHelper.Mode="Underline" />

 <TextBox
    Text="{Binding TotalCount, UpdateSourceTrigger=PropertyChanged}"
    local:NumberFormatHelper.Mode="Underline" />

 */

public static class NumberFormatHelper
{
    public static NumberFormatMode GetMode(DependencyObject obj)
        => (NumberFormatMode)obj.GetValue(ModeProperty);

    public static void SetMode(DependencyObject obj, NumberFormatMode value)
        => obj.SetValue(ModeProperty, value);

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(NumberFormatMode),
            typeof(NumberFormatHelper),
            new PropertyMetadata(NumberFormatMode.None, OnModeChanged));

    #region 数据结构定义

    public enum NumberFormatMode
    {
        None,
        Underline,     // 1_000_000
        Culture        // 1,000,000
    }

    public interface INumberFormatService
    {
        /// <summary>
        /// 原始数字字符串转为格式化显示，"1000" → "1,000"、"1000" → "1_000"
        /// </summary>
        /// <param name="raw"></param>
        /// <returns></returns>
        string Format(string raw);

        /// <summary>
        /// 格式化字符串转回原始数字，"1,000" → "1000"、"1_000" → "1000"
        /// </summary>
        /// <param name="formatted"></param>
        /// <returns></returns>
        string RemoveFormat(string formatted);
    }

    public sealed class CultureNumberFormatService : INumberFormatService
    {
        private readonly CultureInfo culture;
        private readonly int decimalDigits;

        public CultureNumberFormatService(
            CultureInfo? culture = null,
            int decimalDigits = 0)
        {
            this.culture = culture ?? CultureInfo.CurrentCulture;
            this.decimalDigits = decimalDigits;
        }

        public string Format(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            // 去掉已有分隔符（防止重复格式化）
            raw = RemoveFormat(raw);

            if (!decimal.TryParse(raw, NumberStyles.Any, culture, out var value))
                return raw;

            var format = decimalDigits > 0
                ? "N" + decimalDigits
                : "N0";

            return value.ToString(format, culture);
        }

        public string RemoveFormat(string formatted)
        {
            if (string.IsNullOrWhiteSpace(formatted))
                return formatted;

            var groupSeparator = culture.NumberFormat.NumberGroupSeparator;
            return formatted.Replace(groupSeparator, "");
        }
    }

    /// <summary>
    /// 下划线格式实现
    /// </summary>
    public sealed class UnderlineNumberFormatService : INumberFormatService
    {
        public string Format(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            // 去掉已有分隔符（防止重复格式化）
            raw = RemoveFormat(raw);

            // 直接处理字符串，避免数值类型溢出
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && (raw.Length - i) % 3 == 0)
                    sb.Append("_");

                sb.Append(raw[i]);
            }

            return sb.ToString();
        }

        public string RemoveFormat(string formatted)
            => formatted?.Replace("_", "") ?? string.Empty;
    }

    #endregion 数据结构定义

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not NumberFormatMode mode || mode == NumberFormatMode.None)
            return;

        var service = CreateService(mode);

        switch (d)
        {
            case TextBlock tb:
                ApplyToTextBlock(tb, service);
                // 如果当前没有绑定，添加Loaded事件处理程序，在控件加载完成后再次尝试
                tb.Loaded += (sender, args) => ApplyToTextBlock((TextBlock)sender, service);
                break;

            case TextBox box:
                ApplyToTextBox(box, service);
                // 如果当前没有绑定，添加Loaded事件处理程序，在控件加载完成后再次尝试
                box.Loaded += (sender, args) => ApplyToTextBox((TextBox)sender, service);
                break;

            case Button button:
                ApplyToButton(button, service);
                // 如果当前没有绑定，添加Loaded事件处理程序，在控件加载完成后再次尝试
                button.Loaded += (sender, args) => ApplyToButton((Button)sender, service);
                break;

        }
    }

    private static INumberFormatService CreateService(NumberFormatMode mode)
        => mode switch
        {
            NumberFormatMode.Underline => new UnderlineNumberFormatService(),
            NumberFormatMode.Culture => new CultureNumberFormatService(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private static void ApplyToTextBlock(TextBlock tb, INumberFormatService service)
    {
        var binding = BindingOperations.GetBinding(tb, TextBlock.TextProperty);
        if (binding == null)
        {
            // 如果没有绑定，直接格式化当前文本
            tb.Text = service.Format(tb.Text);
            return;
        }

        // 检查是否已经应用了格式化（避免重复应用）
        if (binding.Converter is NumberDisplayConverter existingConverter)
        {
            // 如果已经应用了相同的格式化，直接返回
            if (existingConverter.FormatService != null && existingConverter.FormatService.GetType() == service.GetType())
            {
                return;
            }
        }

        var newBinding = new Binding
        {
            Path = binding.Path,
            Converter = new NumberDisplayConverter
            {
                FormatService = service
            }
        };

        // 复制原始绑定的所有属性
        if (binding.Source != null)
        {
            newBinding.Source = binding.Source;
        }
        else if (!string.IsNullOrEmpty(binding.ElementName))
        {
            newBinding.ElementName = binding.ElementName;
        }
        else if (binding.RelativeSource != null)
        {
            newBinding.RelativeSource = binding.RelativeSource;
        }
        newBinding.Mode = binding.Mode;
        newBinding.UpdateSourceTrigger = binding.UpdateSourceTrigger;
        newBinding.StringFormat = binding.StringFormat;
        newBinding.FallbackValue = binding.FallbackValue;
        newBinding.TargetNullValue = binding.TargetNullValue;

        BindingOperations.SetBinding(tb, TextBlock.TextProperty, newBinding);
    }

    private static void ApplyToTextBox(TextBox tb, INumberFormatService service)
    {
        // 1. 添加 Behavior
        var behaviors = Interaction.GetBehaviors(tb);
        if (!behaviors.OfType<NumericFormatBehavior>().Any())
        {
            behaviors.Add(new NumericFormatBehavior
            {
                FormatService = service
            });
        }

        // 2. 处理 Binding Converter (确保 VM 的数据回写正确)
        var binding = BindingOperations.GetBinding(tb, TextBox.TextProperty);
        if (binding == null)
            return;

        // 检查是否已经应用了格式化（避免重复应用）
        if (binding.Converter is NumberDisplayConverter existingConverter)
        {
            if (existingConverter.FormatService != null && existingConverter.FormatService.GetType() == service.GetType())
            {
                return;
            }
        }

        var newBinding = new Binding
        {
            Path = binding.Path,
            Converter = new NumberDisplayConverter
            {
                FormatService = service
            }
        };

        // 复制原始绑定的所有属性
        if (binding.Source != null)
        {
            newBinding.Source = binding.Source;
        }
        else if (!string.IsNullOrEmpty(binding.ElementName))
        {
            newBinding.ElementName = binding.ElementName;
        }
        else if (binding.RelativeSource != null)
        {
            newBinding.RelativeSource = binding.RelativeSource;
        }
        newBinding.Mode = binding.Mode;
        newBinding.UpdateSourceTrigger = binding.UpdateSourceTrigger;
        newBinding.StringFormat = binding.StringFormat;
        newBinding.FallbackValue = binding.FallbackValue;
        newBinding.TargetNullValue = binding.TargetNullValue;

        BindingOperations.SetBinding(tb, TextBox.TextProperty, newBinding);
    }

    private static void ApplyToButton(Button button, INumberFormatService service)
    {
        var binding = BindingOperations.GetBinding(button, ContentControl.ContentProperty);
        if (binding == null)
        {
            // 如果没有绑定，直接格式化当前内容
            if (button.Content is string contentStr)
            {
                button.Content = service.Format(contentStr);
            }
            return;
        }

        // 检查是否已经应用了格式化（避免重复应用）
        if (binding.Converter is NumberDisplayConverter existingConverter)
        {
            // 如果已经应用了相同的格式化，直接返回
            if (existingConverter.FormatService != null && existingConverter.FormatService.GetType() == service.GetType())
            {
                return;
            }
        }

        var newBinding = new Binding
        {
            Path = binding.Path,
            Converter = new NumberDisplayConverter
            {
                FormatService = service
            }
        };

        // 复制原始绑定的所有属性
        if (binding.Source != null)
        {
            newBinding.Source = binding.Source;
        }
        else if (!string.IsNullOrEmpty(binding.ElementName))
        {
            newBinding.ElementName = binding.ElementName;
        }
        else if (binding.RelativeSource != null)
        {
            newBinding.RelativeSource = binding.RelativeSource;
        }
        newBinding.Mode = binding.Mode;
        newBinding.UpdateSourceTrigger = binding.UpdateSourceTrigger;
        newBinding.StringFormat = binding.StringFormat;
        newBinding.FallbackValue = binding.FallbackValue;
        newBinding.TargetNullValue = binding.TargetNullValue;

        BindingOperations.SetBinding(button, ContentControl.ContentProperty, newBinding);
    }


}