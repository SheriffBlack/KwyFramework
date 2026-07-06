using System.Windows;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// Icon 附加属性帮助类
/// 支持在任何控件上设置图标，支持字符串类型（如字体图标）和 Geometry 类型
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// 图标附加属性
    /// </summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(object),
            typeof(IconHelper),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取图标
    /// </summary>
    public static object? GetIcon(DependencyObject obj)
    {
        return obj.GetValue(IconProperty);
    }

    /// <summary>
    /// 设置图标
    /// </summary>
    public static void SetIcon(DependencyObject obj, object? value)
    {
        obj.SetValue(IconProperty, value);
    }
}
