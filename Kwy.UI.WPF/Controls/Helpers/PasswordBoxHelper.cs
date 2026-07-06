using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// PasswordBox 密码绑定帮助类
/// 提供附加属性实现 PasswordBox.Password 的双向绑定
/// </summary>
public static class PasswordBoxHelper
{
    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    /// <summary>
    /// 获取密码
    /// </summary>
    public static string GetPassword(DependencyObject obj)
    {
        return (string?)obj.GetValue(PasswordProperty) ?? string.Empty;
    }

    /// <summary>
    /// 设置密码
    /// </summary>
    public static void SetPassword(DependencyObject obj, string value)
    {
        obj.SetValue(PasswordProperty, value);
    }

    /// <summary>
    /// 密码附加属性
    /// </summary>
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.RegisterAttached(
            "Password",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordPropertyChanged));

    private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
            return;

        EnsurePasswordChangedHandlerAttached(passwordBox);

        // 防止循环更新
        if ((bool)passwordBox.GetValue(IsUpdatingProperty))
            return;

        string newPassword = e.NewValue as string ?? string.Empty;

        // 如果密码框的密码与附加属性的值不同，则更新密码框
        if (passwordBox.Password != newPassword)
        {
            passwordBox.SetValue(IsUpdatingProperty, true);
            passwordBox.Password = newPassword;
            passwordBox.SetValue(IsUpdatingProperty, false);
        }
    }

    private static readonly DependencyProperty IsPasswordChangedHandlerAttachedProperty =
        DependencyProperty.RegisterAttached("IsPasswordChangedHandlerAttached", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    private static bool GetIsPasswordChangedHandlerAttached(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsPasswordChangedHandlerAttachedProperty);
    }

    private static void SetIsPasswordChangedHandlerAttached(DependencyObject obj, bool value)
    {
        obj.SetValue(IsPasswordChangedHandlerAttachedProperty, value);
    }

    private static void EnsurePasswordChangedHandlerAttached(PasswordBox passwordBox)
    {
        if (GetIsPasswordChangedHandlerAttached(passwordBox))
        {
            return;
        }

        passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        passwordBox.Unloaded += PasswordBox_Unloaded;
        SetIsPasswordChangedHandlerAttached(passwordBox, true);
    }

    private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        // 防止循环更新
        if ((bool)passwordBox.GetValue(IsUpdatingProperty))
            return;

        string currentPassword = passwordBox.Password;
        string attachedPassword = GetPassword(passwordBox);

        // 如果密码框的密码与附加属性的值不同，则更新附加属性
        if (currentPassword != attachedPassword)
        {
            passwordBox.SetValue(IsUpdatingProperty, true);
            SetPassword(passwordBox, currentPassword);
            passwordBox.SetValue(IsUpdatingProperty, false);
        }
    }

    private static void PasswordBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
        passwordBox.Unloaded -= PasswordBox_Unloaded;
        SetIsPasswordChangedHandlerAttached(passwordBox, false);
    }

    /// <summary>
    /// 获取是否显示密码
    /// </summary>
    public static bool GetIsPasswordVisible(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsPasswordVisibleProperty);
    }

    /// <summary>
    /// 设置是否显示密码
    /// </summary>
    public static void SetIsPasswordVisible(DependencyObject obj, bool value)
    {
        obj.SetValue(IsPasswordVisibleProperty, value);
    }

    /// <summary>
    /// 是否显示密码附加属性
    /// </summary>
    public static readonly DependencyProperty IsPasswordVisibleProperty =
        DependencyProperty.RegisterAttached(
            "IsPasswordVisible",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false));
}
