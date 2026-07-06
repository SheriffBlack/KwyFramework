using System.Windows;

namespace Kwy.MVVM.WPF.Dialogs;

/// <summary>
/// 对话框附加属性类。
/// 允许在 UserControl (View) 中通过 XAML 声明对话框的样式、启动位置等。
/// 对标 Prism 的 Prism.Services.Dialogs.Dialog 类。
/// </summary>
public static class Dialog
{
    /// <summary>
    /// 设置对话框窗口样式的附加属性。
    /// </summary>
    public static readonly DependencyProperty WindowStyleProperty =
        DependencyProperty.RegisterAttached(
            "WindowStyle",
            typeof(Style),
            typeof(Dialog),
            new PropertyMetadata(null));

    public static Style? GetWindowStyle(DependencyObject obj) => (Style?)obj.GetValue(WindowStyleProperty);

    public static void SetWindowStyle(DependencyObject obj, Style? value) => obj.SetValue(WindowStyleProperty, value);

    /// <summary>
    /// 设置对话框窗口启动位置的附加属性。
    /// </summary>
    public static readonly DependencyProperty WindowStartupLocationProperty =
        DependencyProperty.RegisterAttached(
            "WindowStartupLocation",
            typeof(WindowStartupLocation),
            typeof(Dialog),
            new PropertyMetadata(WindowStartupLocation.CenterScreen));

    public static WindowStartupLocation GetWindowStartupLocation(DependencyObject obj) => (WindowStartupLocation)obj.GetValue(WindowStartupLocationProperty);

    public static void SetWindowStartupLocation(DependencyObject obj, WindowStartupLocation value) => obj.SetValue(WindowStartupLocationProperty, value);
}
