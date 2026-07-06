using System.Globalization;
using System.Windows.Data;

namespace Kwy.UI.WPF.Components.PropertyGrid;

public sealed class GroupWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double availableWidth
            || values[1] is not double ratio
            || double.IsNaN(availableWidth)
            || availableWidth <= 0)
        {
            return double.NaN;
        }

        ratio = Math.Clamp(ratio, 0.1, 1.0);
        double margin = parameter is string text && double.TryParse(text, NumberStyles.Float, culture, out var parsed)
            ? parsed
            : 24.0;

        return Math.Max(240.0, availableWidth * ratio - margin);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
