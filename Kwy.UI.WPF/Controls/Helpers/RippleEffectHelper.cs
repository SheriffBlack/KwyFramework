using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Kwy.UI.WPF.Controls.Helpers;

/// <summary>
/// 水波纹效果辅助类 (真·极限性能释放版)
/// 彻底抛弃 Behavior，按需创建波纹对象，做到 0 实例挂载开销！
/// </summary>
public static class RippleEffectHelper
{
    private static long _lastRippleTicks = 0;
    private static readonly long _cooldownTicks = TimeSpan.FromMilliseconds(50).Ticks;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RippleEffectHelper),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty RippleBrushProperty =
        DependencyProperty.RegisterAttached(
            "RippleBrush",
            typeof(Brush),
            typeof(RippleEffectHelper),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static Brush? GetRippleBrush(DependencyObject obj) => (Brush?)obj.GetValue(RippleBrushProperty);
    public static void SetRippleBrush(DependencyObject obj, Brush? value) => obj.SetValue(RippleBrushProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control) return;

        // 卸载旧事件，防止重复订阅
        control.PreviewMouseLeftButtonDown -= Control_PreviewMouseLeftButtonDown;

        if ((bool)e.NewValue)
        {
            // 仅仅挂载一个静态事件句柄，相比实例化一个 Behavior 内存开销降为 0
            control.PreviewMouseLeftButtonDown += Control_PreviewMouseLeftButtonDown;
        }
    }

    private static void Control_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        long currentTicks = DateTime.UtcNow.Ticks;
        if (currentTicks - _lastRippleTicks < _cooldownTicks) return;
        _lastRippleTicks = currentTicks;

        if (sender is not Control control) return;

        // 【按需查找 Canvas】
        // 只有被点击的那一瞬间，才回去找 Canvas。
        if (control.Template?.FindName("PART_RippleCanvas", control) is not Canvas canvas) return;

        canvas.IsHitTestVisible = false;
        canvas.ClipToBounds = true;

        Point pos = e.GetPosition(control);
        double w = control.ActualWidth;
        double h = control.ActualHeight;
        
        if (w == 0 || h == 0) return;

        // 计算涵盖整个按钮的最大波纹圆半径
        double maxRadius = Math.Sqrt(w * w + h * h);

        // 【按需创建 Ellipse】
        // 只有真正点击了，才生成波纹效果相关的 UI 元素
        var rippleElement = new Ellipse
        {
            Fill = GetRippleBrush(control)
                ?? control.TryFindResource("RippleBrush") as Brush
                ?? Brushes.Transparent,
            Width = maxRadius * 2,
            Height = maxRadius * 2,
            Opacity = 0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        var scaleTransform = new ScaleTransform(0, 0);
        var translateTransform = new TranslateTransform(pos.X - maxRadius, pos.Y - maxRadius);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translateTransform);
        rippleElement.RenderTransform = transformGroup;

        // 添加到画布
        canvas.Children.Add(rippleElement);

        // 【启动轻量级动画】不再创建重量级 Storyboard
        var ease = new CircleEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        var scaleAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease };
        var opacityAnim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(500) };
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.4, KeyTime.FromPercent(0)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0.4, KeyTime.FromPercent(0.5)));
        opacityAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));

        // 动画结束后，自动清理一切
        opacityAnim.Completed += (s, ev) =>
        {
            canvas.Children.Remove(rippleElement);
        };

        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        rippleElement.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }
}
