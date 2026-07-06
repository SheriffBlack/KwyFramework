using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kwy.UI.WPF.Converters;

public class JudgeToBrushConverter : IValueConverter
{
    public static readonly JudgeToBrushConverter Instance = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOk)
        {
            var resourceKey = isOk ? "OKBrush" : "NGBrush";
            return Application.Current?.TryFindResource(resourceKey) as Brush
                ?? Application.Current?.TryFindResource("FallbackBrush") as Brush
                ?? Brushes.Transparent;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
