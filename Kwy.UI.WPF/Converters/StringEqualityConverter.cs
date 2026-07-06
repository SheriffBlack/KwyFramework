using System.Globalization;
using System.Windows.Data;

namespace Kwy.UI.WPF.Converters;

/// <summary>
/// 字符串相等性转换器
/// 用于将SelectedView与导航项的ViewName进行比较
/// </summary>
public class StringEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return false;

        object v1 = values[0];
        object v2 = values[1];

        // 🌟 性能优化 1：引用相等检查（同一对象直接返回，极快）
        if (ReferenceEquals(v1, v2)) return true;

        // 🌟 性能优化 2：空值快速排除
        if (v1 == null || v2 == null) return false;

        // 🌟 性能优化 3：仅对字符串或需要转换的对象进行 Equals 比较
        // OrdinalIgnoreCase 在处理工业级简短标识符时性能极佳且稳定
        return string.Equals(
            v1.ToString(),
            v2.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        // 🌟 完善回写逻辑：如果按钮被选中 (true)，则将参数 (标识值) 发送回第一个绑定源 (ViewModel)
        if (value is bool isChecked && isChecked)
        {
            return new object[] { parameter, Binding.DoNothing };
        }
        
        return new object[] { Binding.DoNothing, Binding.DoNothing };
    }
}