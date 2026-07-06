using System.Globalization;
using System.Windows.Data;
using static Kwy.UI.WPF.Controls.Helpers.NumberFormatHelper;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// TextBlock 用的 Converter（只显示）
/// </summary>
public class NumberDisplayConverter : IValueConverter
{
    public INumberFormatService? FormatService { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return null;

        // 🌟 性能优化：避免盲目调用 ToString()，如果是字符串则直接使用引用
        string raw = value is string s ? s : (value.ToString() ?? string.Empty);
        return FormatService?.Format(raw) ?? value;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string strValue)
        {
            return value;
        }

        var unformatted = FormatService?.RemoveFormat(strValue);
        if (string.IsNullOrEmpty(unformatted))
        {
            return null;
        }

        return unformatted;
    }
}