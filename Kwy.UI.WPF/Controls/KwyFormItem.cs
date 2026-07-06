using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls;

/// <summary>标签位置枚举</summary>
public enum LabelPosition { Left, Right }

/// <summary>
/// 纯粹的表单布局容器，负责 Label 对齐与排版
/// </summary>
public class KwyFormItem : ContentControl
{
    static KwyFormItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyFormItem),
            new FrameworkPropertyMetadata(typeof(KwyFormItem)));
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(KwyFormItem), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelPositionProperty =
        DependencyProperty.Register(nameof(LabelPosition), typeof(LabelPosition), typeof(KwyFormItem), new PropertyMetadata(LabelPosition.Right));

    public LabelPosition LabelPosition
    {
        get => (LabelPosition)GetValue(LabelPositionProperty);
        set => SetValue(LabelPositionProperty, value);
    }

    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(nameof(LabelWidth), typeof(GridLength), typeof(KwyFormItem), new PropertyMetadata(GridLength.Auto));

    public GridLength LabelWidth
    {
        get => (GridLength)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    public static readonly DependencyProperty InputWidthProperty =
        DependencyProperty.Register(nameof(InputWidth), typeof(GridLength), typeof(KwyFormItem), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

    public GridLength InputWidth
    {
        get => (GridLength)GetValue(InputWidthProperty);
        set => SetValue(InputWidthProperty, value);
    }

    public static readonly DependencyProperty InputHeightProperty =
        DependencyProperty.Register(nameof(InputHeight), typeof(GridLength), typeof(KwyFormItem), new PropertyMetadata(GridLength.Auto));

    public GridLength InputHeight
    {
        get => (GridLength)GetValue(InputHeightProperty);
        set => SetValue(InputHeightProperty, value);
    }
}
