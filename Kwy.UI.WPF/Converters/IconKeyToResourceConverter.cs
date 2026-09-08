using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// 将图标资源键转换为应用资源中的图标对象。
/// 支持 IconStyle.xaml 中的字体图标字符串和 Geometry 图标。
/// </summary>
public class IconKeyToResourceConverter : MarkupExtension, IValueConverter
{
    private static IconKeyToResourceConverter? instance;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string iconKey || string.IsNullOrWhiteSpace(iconKey))
        {
            return null;
        }

        try
        {
            var resource = Application.Current?.TryFindResource(iconKey);
            if (resource != null)
            {
                return resource;
            }
        }
        catch
        {
            // Ignore resource lookup failures. The control will render without an icon.
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return instance ??= new IconKeyToResourceConverter();
    }
}
