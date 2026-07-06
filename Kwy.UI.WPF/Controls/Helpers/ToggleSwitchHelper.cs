using System.Windows;
using System.Windows.Controls.Primitives;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// ToggleSwitch 附加属性助手。
/// 使任意 ToggleButton / CheckBox 能以"拨码开关"的视觉样式出现，
/// 而无需继承或额外封装控件。
///
/// 用法：
///   <!-- 最简单：为原生 ToggleButton 套上开关外观 -->
///   <ToggleButton IsChecked="{Binding EnableOVC}"
///                 helpers:ToggleSwitchHelper.IsSwitch="True" />
///
///   <!-- 嵌套在 KwyFormItem 中，实现带 Label 的表单行 -->
///   <controls:KwyFormItem Label="热电动势补偿">
///       <ToggleButton IsChecked="{Binding EnableOVC}"
///                     helpers:ToggleSwitchHelper.IsSwitch="True" />
///   </controls:KwyFormItem>
///
///   <!-- 切换为下方带文字的开关样式 -->
///   <ToggleButton IsChecked="{Binding EnableOVC}"
///                 helpers:ToggleSwitchHelper.StyleKey="ToggleButtonSwitchBottomContentStyle"
///                 Content="OVC" />
/// </summary>
public static class ToggleSwitchHelper
{
    // ── IsSwitch ─────────────────────────────────────────────────────────
    /// <summary>
    /// 设为 True 时，自动把 ToggleButtonSwitchNoContentStyle 应用到目标控件。
    /// </summary>
    public static readonly DependencyProperty IsSwitchProperty =
        DependencyProperty.RegisterAttached(
            "IsSwitch",
            typeof(bool),
            typeof(ToggleSwitchHelper),
            new PropertyMetadata(false, OnIsSwitchChanged));

    public static bool GetIsSwitch(DependencyObject obj) => (bool)obj.GetValue(IsSwitchProperty);
    public static void SetIsSwitch(DependencyObject obj, bool value) => obj.SetValue(IsSwitchProperty, value);

    private static void OnIsSwitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton tb) return;

        if ((bool)e.NewValue)
        {
            // 读取 StyleKey（如果用户没有指定，用默认值）
            string key = GetStyleKey(tb);
            ApplyStyle(tb, key);
        }
        else
        {
            // 还原为默认隐式样式
            tb.ClearValue(FrameworkElement.StyleProperty);
        }
    }

    // ── StyleKey ─────────────────────────────────────────────────────────
    /// <summary>
    /// 要应用的样式资源键名称。
    /// 默认为 "ToggleButtonSwitchNoContentStyle"（无文字的小拨码开关）。
    /// 可设为 "ToggleButtonSwitchBottomContentStyle" 来显示下方文字。
    /// </summary>
    public static readonly DependencyProperty StyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "StyleKey",
            typeof(string),
            typeof(ToggleSwitchHelper),
            new PropertyMetadata("ToggleButtonSwitchNoContentStyle", OnStyleKeyChanged));

    public static string GetStyleKey(DependencyObject obj) => (string)obj.GetValue(StyleKeyProperty);
    public static void SetStyleKey(DependencyObject obj, string value) => obj.SetValue(StyleKeyProperty, value);

    private static void OnStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton tb) return;

        // 只有在 IsSwitch = true 时才重新应用
        if (GetIsSwitch(tb))
            ApplyStyle(tb, (string)e.NewValue);
    }

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static void ApplyStyle(ToggleButton tb, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        // 等到控件进入可视树后再查资源（否则 Application.Current 可能还没就绪）
        if (tb.IsLoaded)
        {
            DoApply(tb, key);
        }
        else
        {
            // 延迟到 Loaded 事件
            tb.Loaded -= OnToggleButtonLoaded;
            tb.Loaded += OnToggleButtonLoaded;
        }
    }

    private static void OnToggleButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb) return;
        tb.Loaded -= OnToggleButtonLoaded;

        if (GetIsSwitch(tb))
            DoApply(tb, GetStyleKey(tb));
    }

    private static void DoApply(ToggleButton tb, string key)
    {
        var style = tb.TryFindResource(key) as Style
                 ?? Application.Current?.TryFindResource(key) as Style;

        if (style != null)
            tb.Style = style;
    }
}
