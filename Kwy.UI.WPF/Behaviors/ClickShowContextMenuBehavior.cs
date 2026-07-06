using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 左键点击显示上下文菜单的行为。
/// 可适配所有带 ContextMenu 的 FrameworkElement（Button、ListBoxItem、Grid 等）
/// </summary>
public class ClickShowContextMenuBehavior : Behavior<FrameworkElement>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        // 弹出菜单
        AssociatedObject.PreviewMouseLeftButtonDown += AssociatedObject_PreviewMouseLeftButtonDown;

        // 绑定右键预览事件，禁用右键菜单
        AssociatedObject.PreviewMouseRightButtonDown += AssociatedObject_PreviewMouseRightButtonDown;
        // 添加对右键释放事件的处理，确保彻底阻止右键菜单
        AssociatedObject.PreviewMouseRightButtonUp += AssociatedObject_PreviewMouseRightButtonUp;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseLeftButtonDown -= AssociatedObject_PreviewMouseLeftButtonDown;
        AssociatedObject.PreviewMouseRightButtonDown -= AssociatedObject_PreviewMouseRightButtonDown;
        AssociatedObject.PreviewMouseRightButtonUp -= AssociatedObject_PreviewMouseRightButtonUp;
        base.OnDetaching();
    }

    private void AssociatedObject_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var fe = AssociatedObject;
        var menu = fe.ContextMenu;
        if (menu == null) return;

        menu.PlacementTarget = fe;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;

        e.Handled = true;
    }

    private void AssociatedObject_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void AssociatedObject_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

}
