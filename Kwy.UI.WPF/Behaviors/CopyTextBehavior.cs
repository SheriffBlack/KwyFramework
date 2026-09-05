using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Behaviors;

/// <summary>
/// 通用复制行为：绑定到 Button，指定要复制的文本源。
/// 行为层不决定提示方式；复制失败时通过路由事件交给宿主处理。
/// </summary>
public class CopyTextBehavior : Behavior<Button>
{
    private const string CopyFailedEventName = "CopyFailed";

    public static readonly RoutedEvent CopyFailedEvent = EventManager.RegisterRoutedEvent(
        CopyFailedEventName,
        RoutingStrategy.Bubble,
        typeof(EventHandler<CopyTextFailedEventArgs>),
        typeof(CopyTextBehavior));

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
            AssociatedObject.RaiseEvent(new CopyTextFailedEventArgs(CopyFailedEvent, AssociatedObject, ex));
        }
    }
}

public sealed class CopyTextFailedEventArgs : RoutedEventArgs
{
    public CopyTextFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception)
        : base(routedEvent, source)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public Exception Exception { get; }
}
