using System.Windows;

namespace KwyTemplate.Vision.Controls;

/// <summary>
/// 解决 AvalonDock 嵌套 View 时 DataContext 丢失的问题，绑定代理模式
/// </summary>
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
}
