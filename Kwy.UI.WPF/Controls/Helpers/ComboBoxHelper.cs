using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// ComboBox 主题样式附加属性帮助类。
/// 提供图标成员路径和基于图标状态的样式选择；数据绑定仍使用 ComboBox 原生属性。
///
/// 用法：
/// <code><![CDATA[
///   <!-- 原生数据绑定 -->
///   <ComboBox ItemsSource="{Binding Speeds}"
///             SelectedItem="{Binding CurrentSpeed}" />
///
///   <!-- 带图标的下拉框（图标自动切换为 IconComboBoxStyle） -->
///   <ComboBox ItemsSource="{Binding Speeds}"
///             SelectedItem="{Binding CurrentSpeed}"
///             helpers:IconHelper.Icon="&#xE700;" />
///
///   <!-- 通过资源键选择样式 -->
///   <controls:KwyFormItem Label="量程">
///       <ComboBox ItemsSource="{Binding Ranges}"
///                 SelectedItem="{Binding CurrentRange}"
///                 IsEditable="True"
///                 helpers:ComboBoxHelper.StyleKey="DefaultComboBoxStyle" />
///   </controls:KwyFormItem>
/// ]]></code>
/// </summary>
public static class ComboBoxHelper
{
    /// <summary>
    /// 获取用于从所选项目中获取图标的显式属性路径。
    /// 空值将禁用模型属性的查找。
    /// </summary>
    public static readonly DependencyProperty IconMemberPathProperty =
        DependencyProperty.RegisterAttached(
            "IconMemberPath",
            typeof(string),
            typeof(ComboBoxHelper),
            new PropertyMetadata(string.Empty));

    public static string GetIconMemberPath(DependencyObject obj)
        => (string)obj.GetValue(IconMemberPathProperty);

    public static void SetIconMemberPath(DependencyObject obj, string value)
        => obj.SetValue(IconMemberPathProperty, value);

    // ── StyleKey ─────────────────────────────────────────────────────────
    /// <summary>
    /// 指定要应用的 ComboBox 样式资源键。
    /// 默认 "DefaultComboBoxStyle"；带图标时自动变为 "IconComboBoxStyle"。
    /// </summary>
    public static readonly DependencyProperty StyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "StyleKey",
            typeof(object),
            typeof(ComboBoxHelper),
            new PropertyMetadata(KwyResourceKeys.DefaultComboBoxStyle, OnStyleKeyChanged));

    public static object? GetStyleKey(DependencyObject obj) => obj.GetValue(StyleKeyProperty);
    public static void SetStyleKey(DependencyObject obj, object? value) => obj.SetValue(StyleKeyProperty, value);

    private static void OnStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox cb || GetAutoStyle(cb)) return;
        if (e.NewValue is object key)
        {
            ApplyStyle(cb, key);
        }
        else
        {
            StyleApplicationHelper.RestoreStyle(cb);
        }
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
        if (d is not ComboBox cb) return;
        if ((bool)e.NewValue)
        {
            ApplyAutoStyle(cb);
        }
        else if (GetStyleKey(cb) is object key)
        {
            ApplyStyle(cb, key);
        }
        else
        {
            StyleApplicationHelper.RestoreStyle(cb);
        }
    }

    internal static void OnIconChanged(ComboBox comboBox)
    {
        if (GetAutoStyle(comboBox))
        {
            ApplyAutoStyle(comboBox);
        }
    }

    private static void ApplyAutoStyle(ComboBox cb)
    {
        var icon = IconHelper.GetIcon(cb);
        object key = icon != null ? KwyResourceKeys.IconComboBoxStyle : KwyResourceKeys.DefaultComboBoxStyle;
        ApplyStyle(cb, key);
    }

    // ── 内部工具 ─────────────────────────────────────────────────────────

    private static void ApplyStyle(ComboBox cb, object key)
    {
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
        if (GetAutoStyle(cb))
        {
            ApplyAutoStyle(cb);
        }
        else if (GetStyleKey(cb) is object key)
        {
            DoApply(cb, key);
        }
    }

    private static void DoApply(ComboBox cb, object key)
    {
        var style = cb.TryFindResource(key) as Style
                 ?? Application.Current?.TryFindResource(key) as Style;
        if (style != null) StyleApplicationHelper.ApplyStyle(cb, style);
    }
}
