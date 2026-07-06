using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 通用复制行为：绑定到Button，指定要复制的文本源
/// </summary>
public class CopyTextBehavior : Behavior<Button>
{
    // 依赖属性：要复制的文本（绑定到ModbusModel.ModbusCommand）
    public static readonly DependencyProperty TextToCopyProperty =
        DependencyProperty.Register(
            nameof(TextToCopy),
            typeof(string),
            typeof(CopyTextBehavior),
            new PropertyMetadata(string.Empty));

    public string TextToCopy
    {
        get => (string)GetValue(TextToCopyProperty);
        set => SetValue(TextToCopyProperty, value);
    }

    // 行为附加到Button时绑定Click事件
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Click += OnButtonClick;
    }

    // 行为脱离时解绑事件
    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Click -= OnButtonClick;
    }

    // 复制逻辑
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextToCopy))
            {
                Clipboard.SetText(TextToCopy);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败：{ex.Message}", "错误");
        }
    }
}