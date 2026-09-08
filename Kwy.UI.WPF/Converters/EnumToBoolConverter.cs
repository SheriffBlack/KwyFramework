using System.Globalization;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

public sealed class EnumToBoolConverter : IValueConverter
{
    // 将枚举值转换为 bool（是否与参数匹配）
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // 🌟 性能优化 1：同引用直接返回
        if (ReferenceEquals(value, parameter)) return true;

        // 🌟 兼容性优化：如果参数是字符串（XAML中直接输入内容），则进行不区分大小写的比对
        if (parameter is string paramStr)
        {
            return string.Equals(value.ToString(), paramStr, StringComparison.OrdinalIgnoreCase);
        }

        return value.Equals(parameter);
    }

    // 将 bool 转换为枚举值（选中时返回参数，未选中时不处理）
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool isChecked || parameter == null)
            return Binding.DoNothing;
        if (!isChecked)
            return Binding.DoNothing;

        if (targetType.IsEnum && parameter is string enumName)
        {
            return Enum.TryParse(targetType, enumName, true, out object? parsed)
                ? parsed
                : Binding.DoNothing;
        }

        return targetType.IsInstanceOfType(parameter) ? parameter : Binding.DoNothing;
    }
}
