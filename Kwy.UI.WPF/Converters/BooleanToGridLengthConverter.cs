using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// 参考 MahApps 的实现：使用 bool 控制 RowDefinition/GridLength。
/// true -> TrueLength；false -> FalseLength（默认 0）。
/// </summary>
public class BooleanToGridLengthConverter : IValueConverter
{
    public GridLength TrueLength { get; set; } = new GridLength(2);
    public GridLength FalseLength { get; set; } = new GridLength(0);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 🌟 保持高效的同时增加模式匹配安全性
        return value is bool b && b ? TrueLength : FalseLength;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}