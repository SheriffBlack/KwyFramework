using Kwy.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KwyTemplate.Vision.Converters;

public sealed class InputTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not InputType inputType || parameter is not string expected)
        {
            return Visibility.Collapsed;
        }

        return string.Equals(inputType.ToString(), expected, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
