using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// 分割线类型
/// </summary>
public enum LegendStyle
{
    /// <summary>
    /// 靠左
    /// </summary>
    Left,

    /// <summary>
    /// 居中
    /// </summary>
    Center,

    /// <summary>
    /// 靠右
    /// </summary>
    Right
}

/// <summary>
/// 是控件Content可直接填充内容
/// </summary>
[ContentProperty("Content")]
[DefaultProperty("Content")]
[Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)]
public class KwyLegend : System.Windows.Controls.ContentControl
{
    /// <summary>
    /// 分隔线颜色
    /// </summary>
    [Bindable(true)]
    public Brush LineColor
    {
        get { return (Brush)GetValue(LineColorProperty); }
        set { SetValue(LineColorProperty, value); }
    }

    // Using a DependencyProperty as the backing store for LineColor.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register("LineColor", typeof(Brush), typeof(KwyLegend));

    /// <summary>
    /// 分割线粗细大小
    /// </summary>
    [Bindable(true)]
    public double Line
    {
        get { return (double)GetValue(LineProperty); }
        set { SetValue(LineProperty, value); }
    }

    public static readonly DependencyProperty LineProperty =
        DependencyProperty.Register("Line", typeof(double), typeof(KwyLegend));

    /// <summary>
    /// 类型
    /// </summary>
    [Bindable(true)]
    public LegendStyle Type
    {
        get { return (LegendStyle)GetValue(TypeProperty); }
        set { SetValue(TypeProperty, value); }
    }

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register("Type", typeof(LegendStyle), typeof(KwyLegend));

    /// <summary>
    /// 标题
    /// </summary>
    [Bindable(true)]
    public object Header
    {
        get { return (string)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(object), typeof(KwyLegend));
}