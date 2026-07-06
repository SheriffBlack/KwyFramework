using Kwy.UI.Enums;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using static Kwy.UI.WPF.Controls.KwyTray;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 处理盘孔项右键菜单的行为
/// </summary>
public class PlateItemMenuBehavior : Behavior<ContextMenu>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        // 为菜单项添加事件处理
        AssociatedObject.Opened += ContextMenu_Opened;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Opened -= ContextMenu_Opened;
        base.OnDetaching();
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu contextMenu)
        {
            // 为所有菜单项添加点击事件处理
            foreach (var item in contextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    // 移除旧的事件处理（避免重复添加）
                    menuItem.Click -= MenuItem_Click;
                    menuItem.Click += MenuItem_Click;
                }
            }
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            // 根据菜单项的 Header 判断操作
            string header = menuItem.Header?.ToString() ?? string.Empty;
            TrayItemStatus? targetStatus = null;

            if (header == "OK")
            {
                targetStatus = TrayItemStatus.OK;
            }
            else if (header == "NG")
            {
                targetStatus = TrayItemStatus.NG;
            }
            else if (header == "无料")
            {
                targetStatus = TrayItemStatus.NoMaterial;
            }

            if (targetStatus == null) return;

            // 支持单个项或多项选择
            if (AssociatedObject.DataContext is ITrayItem singleItem)
            {
                // 单个项
                singleItem.Status = targetStatus.Value;
            }
            else if (AssociatedObject.DataContext is System.Collections.IEnumerable items)
            {
                // 多个选中的项（批量操作）
                foreach (var item in items)
                {
                    if (item is ITrayItem plateItem)
                    {
                        plateItem.Status = targetStatus.Value;
                    }
                }
            }
            else if (AssociatedObject.PlacementTarget is FrameworkElement placementTarget)
            {
                // 尝试从 PlacementTarget 获取
                if (placementTarget.DataContext is ITrayItem singlePlateItem)
                {
                    singlePlateItem.Status = targetStatus.Value;
                }
                else if (placementTarget.DataContext is System.Collections.IEnumerable targetItems)
                {
                    // 从 PlacementTarget 获取多个项
                    foreach (var targetItem in targetItems)
                    {
                        if (targetItem is ITrayItem plateItem)
                        {
                            plateItem.Status = targetStatus.Value;
                        }
                    }
                }
            }

            // 操作完成后，关闭菜单（这会触发 Closed 事件，移除选择框）
            if (AssociatedObject.IsOpen)
            {
                AssociatedObject.IsOpen = false;
            }
        }
    }
}