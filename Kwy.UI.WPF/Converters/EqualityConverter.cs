using System.Globalization;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// Compares two binding values using their native equality semantics.
/// </summary>
public sealed class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(static _ => Binding.DoNothing).ToArray();
}
