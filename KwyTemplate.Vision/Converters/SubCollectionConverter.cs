using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace KwyTemplate.Vision.Converters;

/// <summary>
/// 将集合按指定的起始索引和数量切片。
/// 用于将单一的 InputPorts/OutputPorts 集合分配到芯片的四个边（左、顶、右、底）。
/// </summary>
public class SubCollectionConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable source) return null;

        var list = source.Cast<object>().ToList();
        if (parameter is not string paramStr) return list;

        // 参数格式: "startIndex, count"
        var parts = paramStr.Split(',');
        if (parts.Length != 2) return list;

        if (int.TryParse(parts[0].Trim(), out int start) && int.TryParse(parts[1].Trim(), out int count))
        {
            if (start >= list.Count) return Enumerable.Empty<object>();
            return list.Skip(start).Take(count).ToList();
        }

        return list;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
