using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// ComboBox 附加属性助手。
/// 使原生 ComboBox 无需继承即可获得 ItemsSource、SelectedItem、
/// Icon 图标、IsEditable 等能力的快捷绑定支持。
///
/// 用法：
///   <!-- 最简单的下拉框，带主题样式 -->
///   <ComboBox ItemsSource="{Binding Speeds}"
///             SelectedItem="{Binding CurrentSpeed}" />
///
///   <!-- 带图标的下拉框（图标自动切换为 IconComboBoxStyle） -->
///   <ComboBox ItemsSource="{Binding Speeds}"
///             SelectedItem="{Binding CurrentSpeed}"
///             helpers:IconHelper.Icon="&#xE700;" />
///
///   <!-- 可编辑 + 带单位显示（嵌套在 KwyFormItem 中） -->
///   <controls:KwyFormItem Label="量程">
///       <ComboBox ItemsSource="{Binding Ranges}"
///                 SelectedItem="{Binding CurrentRange}"
///                 IsEditable="True"
///                 helpers:ComboBoxHelper.StyleKey="DefaultComboBoxStyle" />
///   </controls:KwyFormItem>
/// </summary>
public static class ComboBoxHelper
{
    // ── StyleKey ─────────────────────────────────────────────────────────
    /// <summary>
    /// 指定要应用的 ComboBox 样式资源键。
    /// 默认 "DefaultComboBoxStyle"；带图标时自动变为 "IconComboBoxStyle"。
    /// </summary>
    public static readonly DependencyProperty StyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "StyleKey",
            typeof(string),
            typeof(ComboBoxHelper),
            new PropertyMetadata("DefaultComboBoxStyle", OnStyleKeyChanged));

    public static string GetStyleKey(DependencyObject obj) => (string)obj.GetValue(StyleKeyProperty);
    public static void SetStyleKey(DependencyObject obj, string value) => obj.SetValue(StyleKeyProperty, value);

    private static void OnStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ComboBox cb && e.NewValue is string key)
            ApplyStyle(cb, key);
    }

    // ── AutoStyle（自动根据有无 Icon 选择样式） ───────────────────────────
    /// <summary>
    /// 设为 True 时，控件将根据 IconHelper.Icon 是否存在自动选择样式。
    /// </summary>
    public static readonly DependencyProperty AutoStyleProperty =
        DependencyProperty.RegisterAttached(
            "AutoStyle",
            typeof(bool),
            typeof(ComboBoxHelper),
            new PropertyMetadata(false, OnAutoStyleChanged));

    public static bool GetAutoStyle(DependencyObject obj) => (bool)obj.GetValue(AutoStyleProperty);
    public static void SetAutoStyle(DependencyObject obj, bool value) => obj.SetValue(AutoStyleProperty, value);

    private static void OnAutoStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox cb || !(bool)e.NewValue) return;
        ApplyAutoStyle(cb);

        // 监听 Icon 属性变化（设置 Icon 可能在 AutoStyle 之后）
        cb.SetBinding(IconHelper.IconProperty, new Binding
        {
            Source = cb,
            Path   = new PropertyPath(IconHelper.IconProperty),
            Mode   = BindingMode.OneWay
        });
    }

    private static void ApplyAutoStyle(ComboBox cb)
    {
        var icon = IconHelper.GetIcon(cb);
        string key = icon != null ? "IconComboBoxStyle" : "DefaultComboBoxStyle";
        ApplyStyle(cb, key);
    }

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static void ApplyStyle(ComboBox cb, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (cb.IsLoaded)
        {
            DoApply(cb, key);
        }
        else
        {
            cb.Loaded -= OnLoaded;
            cb.Loaded += OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        cb.Loaded -= OnLoaded;
        DoApply(cb, GetStyleKey(cb));
    }

    private static void DoApply(ComboBox cb, string key)
    {
        var style = cb.TryFindResource(key) as Style
                 ?? Application.Current?.TryFindResource(key) as Style;
        if (style != null) cb.Style = style;
    }
}
