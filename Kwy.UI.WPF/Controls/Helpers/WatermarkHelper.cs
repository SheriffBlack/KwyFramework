using System.Windows;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// 水印辅助类
/// 用于在 ComboBox、TextBox 等控件上设置水印文本
/// </summary>
public static class WatermarkHelper
{
    /// <summary>
    /// 水印文本附加属性
    /// </summary>
    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.RegisterAttached(
            "Watermark",
            typeof(string),
            typeof(WatermarkHelper),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取水印文本
    /// </summary>
    public static string? GetWatermark(DependencyObject obj)
    {
        return (string?)obj.GetValue(WatermarkProperty);
    }

    /// <summary>
    /// 设置水印文本
    /// </summary>
    public static void SetWatermark(DependencyObject obj, string? value)
    {
        obj.SetValue(WatermarkProperty, value);
    }
}