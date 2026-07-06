using System.Windows;
using System.Windows.Controls;

namespace Kwy.UI.WPF.Controls;

/*
项 1 位置	StartAngle值（弧度）	      近似值
12 点	    -Math.PI/2 -1.5708
1 点	    -Math.PI/2 + Math.PI/12	     -1.308997
2 点	    -Math.PI/2 + Math.PI/6	     -1.0472
3 点	    0	                          0
4 点	    Math.PI/6	                  0.5236
5 点	    Math.PI/3	                  1.0472
6 点	    Math.PI/2	                  1.5708
7 点	    Math.PI/3 * 2	              2.0944
8 点	    Math.PI/6 * 5	              2.61799
9 点	    Math.PI	                      3.14159

 */

/// <summary>
/// 圆形布局面板
/// </summary>
public class KwyCirclePanel : Panel
{
    //起始角度依赖属性（单位：弧度）
    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(
            "StartAngle",
            typeof(double),
            typeof(KwyCirclePanel),
            new PropertyMetadata(-Math.PI / 2, OnLayoutPropertyChanged)); // 默认-π/2（12点）

    // 依赖属性：圆的半径（可在XAML中设置）
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(
            "Radius",
            typeof(double),
            typeof(KwyCirclePanel),
            new PropertyMetadata(200.0, OnLayoutPropertyChanged));

    // 依赖属性：圆心X坐标（默认面板中心）
    public static readonly DependencyProperty CenterXProperty =
        DependencyProperty.Register(
            "CenterX",
            typeof(double),
            typeof(KwyCirclePanel),
            new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    // 依赖属性：圆心Y坐标（默认面板中心）
    public static readonly DependencyProperty CenterYProperty =
        DependencyProperty.Register(
            "CenterY",
            typeof(double),
            typeof(KwyCirclePanel),
            new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

    //StartAngle属性封装，开始角度
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    // 半径属性封装
    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    // 圆心X属性封装
    public double CenterX
    {
        get => (double)GetValue(CenterXProperty);
        set => SetValue(CenterXProperty, value);
    }

    // 圆心Y属性封装
    public double CenterY
    {
        get => (double)GetValue(CenterYProperty);
        set => SetValue(CenterYProperty, value);
    }

    // 布局属性变化时刷新布局
    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((KwyCirclePanel)d).InvalidateArrange();
    }

    /// <summary>
    /// 测量子项尺寸（保证子项能正常显示）
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        // 给每个子项分配最大可用空间，让子项自己测量尺寸
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(availableSize);
        }
        // 返回面板的默认尺寸（可根据需要调整）
        return new Size(Radius * 2, Radius * 2);
    }

    /// <summary>
    /// 核心：排列子项到圆形位置
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        int childCount = InternalChildren.Count;
        if (childCount == 0) return finalSize;

        // 计算圆心（如果没设置，默认面板中心）
        double centerX = double.IsNaN(CenterX) ? finalSize.Width / 2 : CenterX;
        double centerY = double.IsNaN(CenterY) ? finalSize.Height / 2 : CenterY;

        // 每个子项的角度间隔（360度均分）
        double angleStep = 2 * Math.PI / childCount;

        // 起始角度,用依赖属性的StartAngle
        double startAngle = StartAngle;

        for (int i = 0; i < childCount; i++)
        {
            UIElement child = InternalChildren[i];
            if (child.Visibility == Visibility.Collapsed) continue;

            // 计算当前子项的角度（弧度）
            double angle = startAngle + i * angleStep;
            // 三角函数计算子项的左上角坐标（WPF坐标系：Y轴向下，所以sin取负）
            double x = centerX + Radius * Math.Cos(angle) - child.DesiredSize.Width / 2;
            double y = centerY + Radius * Math.Sin(angle) - child.DesiredSize.Height / 2;

            // 排列子项到计算出的位置
            child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
        }

        return finalSize;
    }
}