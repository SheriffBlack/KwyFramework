using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kwy.UI.WPF.Converters;

public class BoolToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, Brush> brushCache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && parameter is string brushes)
        {
            // Cache parsed colors because converter parameters are commonly reused.
            var brushList = brushes.Split(',');
            if (brushList.Length < 2) return GetFallbackBrush();

            string colorKey = isActive ? brushList[0].Trim() : brushList[1].Trim();

            return brushCache.GetOrAdd(colorKey, key =>
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(key);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return GetFallbackBrush();
                }
            });
        }

        return GetFallbackBrush();
    }

    private static Brush GetFallbackBrush()
        => Application.Current?.TryFindResource("FallbackBrush") as Brush ?? Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
