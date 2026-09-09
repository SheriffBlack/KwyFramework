using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Percent;

internal sealed class PercentCountTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not double current || values[1] is not double total || values[2] is not string format)
        {
            return string.Empty;
        }

        try
        {
            return string.Format(culture, format, current, total);
        }
        catch (FormatException)
        {
            return string.Format(culture, "{0} / {1}", current, total);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(static _ => DependencyProperty.UnsetValue).ToArray();
}
