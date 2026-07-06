using Kwy.UI.WPF.Behaviors;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// ListBox 附加属性助手。
/// 提供自动滚动、交替行色、自定义样式等能力，无需继承 ListBox。
///
/// 用法：
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
            lb.ClearValue(ListBox.ItemContainerStyleProperty);
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
        var style = lb.TryFindResource("ListBoxItemStyle") as Style
                 ?? Application.Current?.TryFindResource("ListBoxItemStyle") as Style;
        if (style != null)
            lb.ItemContainerStyle = style;
    }

    // ── AutoScroll ───────────────────────────────────────────────────────
    /// <summary>
    /// 设为 True 时，自动将 AutoScrollItemsControlBehavior 附加到 ListBox，
    /// 使其在新增项时始终滚动到底部。
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
            typeof(string),
            typeof(ListBoxHelper),
            new PropertyMetadata(null, OnStyleKeyChanged));

    public static string? GetStyleKey(DependencyObject obj) => (string?)obj.GetValue(StyleKeyProperty);
    public static void SetStyleKey(DependencyObject obj, string? value) => obj.SetValue(StyleKeyProperty, value);

    private static void OnStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox lb || e.NewValue is not string key) return;
        if (lb.IsLoaded) DoApplyStyle(lb, key);
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
        if (!string.IsNullOrEmpty(key)) DoApplyStyle(lb, key!);
    }

    private static void DoApplyStyle(ListBox lb, string key)
    {
        var style = lb.TryFindResource(key) as Style
                 ?? Application.Current?.TryFindResource(key) as Style;
        if (style != null) lb.Style = style;
    }
}
