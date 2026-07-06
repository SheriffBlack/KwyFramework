using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

public class WindowStateToCornerRadiusConverter : IValueConverter
{
    // 缓存CornerRadius对象，避免频繁创建
    private static readonly CornerRadius MaximizedCornerRadius = new CornerRadius(0);

    private static readonly CornerRadius NormalCornerRadius = new CornerRadius(12);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 🌟 性能与安全性平衡：利用模式匹配快速识别状态
        return value is WindowState state && state == WindowState.Maximized ? MaximizedCornerRadius : NormalCornerRadius;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}