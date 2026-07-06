using System.Globalization;
using System.Windows.Data;

namespace KwyTemplate.Vision.Converters;

public class EqualityToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return System.Windows.Visibility.Collapsed;
        bool equals = Equals(values[0], values[1]);
        return equals ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
