using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Behaviors;

public class CloseDialogBehavior : Behavior<Button>
{
    protected override void OnAttached()
    {
        AssociatedObject.Click += OnClick;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Click -= OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(AssociatedObject);
        if (window != null)
        {
            // 对于对话框，设置 DialogResult
            if (window is Window dialogWindow)
            {
                // 检查是否可以设置 DialogResult（只有通过 ShowDialog() 显示的窗口才能设置）
                try
                {
                    dialogWindow.DialogResult = false; // 取消
                }
                catch
                {
                    // 如果不是通过 ShowDialog() 显示的，直接关闭
                    dialogWindow.Close();
                }
            }
            else
            {
                // 对于普通窗口，直接关闭
                window.Close();
            }
        }
    }
}