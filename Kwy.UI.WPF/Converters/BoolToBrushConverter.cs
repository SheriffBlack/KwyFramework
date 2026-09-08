using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Kwy.UI.WPF.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }

    public Brush? FalseBrush { get; set; }

    public Brush FallbackBrush { get; set; } = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag
            ? (flag ? TrueBrush : FalseBrush) ?? FallbackBrush
            : FallbackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
