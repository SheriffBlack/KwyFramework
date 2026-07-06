using System.Windows.Data;

namespace KwyTemplate.Vision.Converters;

public class MathConverter : IMultiValueConverter
{
    public static MathConverter Average { get; } = new MathConverter();

    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;
        try
        {
            double sum = 0;
            int count = 0;
            foreach (var v in values)
            {
                if (v is double d) { sum += d; count++; }
            }
            return count > 0 ? sum / count : 0.0;
        }
        catch { return 0.0; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
