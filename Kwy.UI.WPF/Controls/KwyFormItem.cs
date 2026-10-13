using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls;

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

    public static readonly DependencyProperty LabelTextAlignmentProperty =
        DependencyProperty.Register(nameof(LabelTextAlignment), typeof(TextAlignment), typeof(KwyFormItem), new PropertyMetadata(TextAlignment.Right),
            value => value is TextAlignment alignment && Enum.IsDefined(alignment));

    public TextAlignment LabelTextAlignment
    {
        get => (TextAlignment)GetValue(LabelTextAlignmentProperty);
        set => SetValue(LabelTextAlignmentProperty, value);
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

    /// <summary>标签与内容之间的距离。</summary>
    public static readonly DependencyProperty LabelSpacingProperty =
        DependencyProperty.Register(nameof(LabelSpacing), typeof(double), typeof(KwyFormItem), new PropertyMetadata(15d),
            value => value is double spacing && double.IsFinite(spacing) && spacing >= 0d);

    public double LabelSpacing
    {
        get => (double)GetValue(LabelSpacingProperty);
        set => SetValue(LabelSpacingProperty, value);
    }

    /// <summary>标签列共享尺寸分组；父容器需开启 Grid.IsSharedSizeScope。</summary>
    public string? LabelSharedSizeGroup
    {
        get => (string?)GetValue(LabelSharedSizeGroupProperty);
        set => SetValue(LabelSharedSizeGroupProperty, value);
    }

    public static readonly DependencyProperty LabelSharedSizeGroupProperty =
        DependencyProperty.Register(nameof(LabelSharedSizeGroup), typeof(string), typeof(KwyFormItem), new PropertyMetadata("LabelGroup"));
}
