using Kwy.UI.WPF.Behaviors;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// ListBox 主题与交互附加属性帮助类。
/// 提供主题样式、样式资源键，以及对自动滚动行为的简化入口，无需继承 ListBox。
///
/// 用法：
/// <code><![CDATA[
///   <!-- 最基础：交替行 + 主题 ItemContainerStyle -->
///   <ListBox ItemsSource="{Binding Logs}"
///            helpers:ListBoxHelper.UseTheme="True"
///            AlternationCount="2" />
///
///   <!-- 自动滚动到底部 -->
///   <ListBox ItemsSource="{Binding Logs}"
///            helpers:ListBoxHelper.AutoScroll="True" />
///
///   <!-- 完整配置（嵌套在 KwyFormItem 中） -->
///   <controls:KwyFormItem Label="日志" InputHeight="150">
///       <ListBox ItemsSource="{Binding Logs}"
///                helpers:ListBoxHelper.UseTheme="True"
///                helpers:ListBoxHelper.AutoScroll="True"
///                AlternationCount="2">
///           <ListBox.ItemTemplate>
///               <DataTemplate>
///                   <TextBlock Text="{Binding}" />
///               </DataTemplate>
///           </ListBox.ItemTemplate>
///       </ListBox>
///   </controls:KwyFormItem>
/// ]]></code>
/// </summary>
public static class ListBoxHelper
{
    // ── UseTheme ─────────────────────────────────────────────────────────
    /// <summary>
    /// 设为 True 时，自动将 "ListBoxItemStyle" 应用为 ItemContainerStyle，
    /// 激活交替行色、Hover 高亮、选中左侧条等主题效果。
    /// </summary>
    public static readonly DependencyProperty UseThemeProperty =
        DependencyProperty.RegisterAttached(
            "UseTheme",
            typeof(bool),
            typeof(ListBoxHelper),
            new PropertyMetadata(false, OnUseThemeChanged));

    public static bool GetUseTheme(DependencyObject obj) => (bool)obj.GetValue(UseThemeProperty);
    public static void SetUseTheme(DependencyObject obj, bool value) => obj.SetValue(UseThemeProperty, value);

    private static void OnUseThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox lb) return;

        if ((bool)e.NewValue)
        {
            if (lb.IsLoaded)
                ApplyTheme(lb);
            else
            {
                lb.Loaded -= OnLoaded;
                lb.Loaded += OnLoaded;
            }
        }
        else
        {
            StyleApplicationHelper.RestoreItemContainerStyle(lb);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBox lb) return;
        lb.Loaded -= OnLoaded;
        if (GetUseTheme(lb)) ApplyTheme(lb);
    }

    private static void ApplyTheme(ListBox lb)
    {
        var style = lb.TryFindResource(KwyResourceKeys.ListBoxItemStyle) as Style
                 ?? Application.Current?.TryFindResource(KwyResourceKeys.ListBoxItemStyle) as Style;
        if (style != null)
            StyleApplicationHelper.ApplyItemContainerStyle(lb, style);
    }

    // ── AutoScroll ───────────────────────────────────────────────────────
    /// <summary>
    /// 设为 <see langword="true"/> 时，自动附加 <see cref="AutoScrollItemsControlBehavior"/>。
    /// 适合仅需默认配置的场景；需调整防抖等高级选项时，应直接在 XAML 中使用该行为。
    /// </summary>
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(ListBoxHelper),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox lb) return;

        var behaviors = Interaction.GetBehaviors(lb);
        var existing  = FindAutoScrollBehavior(behaviors);

        if ((bool)e.NewValue)
        {
            if (existing == null)
                behaviors.Add(new AutoScrollItemsControlBehavior());
        }
        else
        {
            if (existing != null)
                behaviors.Remove(existing);
        }
    }

    private static AutoScrollItemsControlBehavior? FindAutoScrollBehavior(BehaviorCollection behaviors)
    {
        foreach (var b in behaviors)
            if (b is AutoScrollItemsControlBehavior a) return a;
        return null;
    }

    // ── StyleKey ─────────────────────────────────────────────────────────
    /// <summary>
    /// 指定要应用的 ListBox 样式资源键（可选）。
    /// </summary>
    public static readonly DependencyProperty StyleKeyProperty =
        DependencyProperty.RegisterAttached(
            "StyleKey",
            typeof(object),
            typeof(ListBoxHelper),
            new PropertyMetadata(null, OnStyleKeyChanged));

    public static object? GetStyleKey(DependencyObject obj) => obj.GetValue(StyleKeyProperty);
    public static void SetStyleKey(DependencyObject obj, object? value) => obj.SetValue(StyleKeyProperty, value);

    private static void OnStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox lb) return;
        if (e.NewValue is not object key)
        {
            StyleApplicationHelper.RestoreStyle(lb);
        }
        else if (lb.IsLoaded)
        {
            DoApplyStyle(lb, key);
        }
        else
        {
            lb.Loaded -= OnStyleLoaded;
            lb.Loaded += OnStyleLoaded;
        }
    }

    private static void OnStyleLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBox lb) return;
        lb.Loaded -= OnStyleLoaded;
        var key = GetStyleKey(lb);
        if (key != null) DoApplyStyle(lb, key);
    }

    private static void DoApplyStyle(ListBox lb, object key)
    {
        var style = lb.TryFindResource(key) as Style
                 ?? Application.Current?.TryFindResource(key) as Style;
        if (style != null) StyleApplicationHelper.ApplyStyle(lb, style);
    }
}
