using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Percent;

internal sealed class PercentValueTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double percentage || values[1] is not string format)
        {
            return string.Empty;
        }

        try
        {
            return percentage.ToString(format, culture);
        }
        catch (FormatException)
        {
            return percentage.ToString("P2", culture);
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(static _ => DependencyProperty.UnsetValue).ToArray();
}
