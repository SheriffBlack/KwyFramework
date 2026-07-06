using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

[TemplatePart(Name = IndicatorPartName, Type = typeof(FrameworkElement))]
public class KwyPercent : Control
{
    private const string IndicatorPartName = "PART_Indicator";
    private FrameworkElement? indicator;

    static KwyPercent()
    {
        // 告诉 WPF 从 Generic.xaml 中加载默认样式
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyPercent), new FrameworkPropertyMetadata(typeof(KwyPercent)));
    }

    public KwyPercent()
    {
        // 默认尺寸，对应原 UserControl 的 Width/Height
        // 注意：在实际使用中通常由外部布局决定，这里只是默认建议
        Width = 105;
        Height = 250;
    }

    #region Dependency Properties (Input)

    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total), typeof(int), typeof(KwyPercent),
        new PropertyMetadata(0, OnDataChanged));

    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current), typeof(int), typeof(KwyPercent),
        new PropertyMetadata(0, OnDataChanged));

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public int Current
    {
        get => (int)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    // 添加一个 BarBrush 属性，让渐变色可以被外部配置（可选优化）
    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(KwyPercent), new PropertyMetadata(null));

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    #endregion Dependency Properties (Input)

    #region Read-Only Dependency Properties (Output for Binding)

    // 使用 DependencyPropertyKey 定义只读依赖属性，防止外部修改计算结果

    private static readonly DependencyPropertyKey PercentageTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(PercentageText), typeof(string), typeof(KwyPercent), new PropertyMetadata("0%"));

    public static readonly DependencyProperty PercentageTextProperty = PercentageTextPropertyKey.DependencyProperty;

    public string PercentageText
    {
        get => (string)GetValue(PercentageTextProperty);
        private set => SetValue(PercentageTextPropertyKey, value);
    }

    private static readonly DependencyPropertyKey CurrentCountTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(CurrentCountText), typeof(string), typeof(KwyPercent), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CurrentCountTextProperty = CurrentCountTextPropertyKey.DependencyProperty;

    public string CurrentCountText
    {
        get => (string)GetValue(CurrentCountTextProperty);
        private set => SetValue(CurrentCountTextPropertyKey, value);
    }

    private static readonly DependencyPropertyKey ComputedTooltipTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(ComputedTooltipText), typeof(string), typeof(KwyPercent), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ComputedTooltipTextProperty = ComputedTooltipTextPropertyKey.DependencyProperty;

    public string ComputedTooltipText
    {
        get => (string)GetValue(ComputedTooltipTextProperty);
        private set => SetValue(ComputedTooltipTextPropertyKey, value);
    }

    #endregion Read-Only Dependency Properties (Output for Binding)

    #region Logic

    // 当 Total 或 Current 改变时触发
    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KwyPercent controls)
        {
            controls.UpdateVisuals();
        }
    }

    // 获取模板中的部件
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        indicator = GetTemplateChild(IndicatorPartName) as FrameworkElement;
        UpdateVisuals();
    }

    // 当控件大小改变时，重新计算高度
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        // 1. 更新文本逻辑 (原 UserControl 代码逻辑移植 [cite: 7, 8])
        // Percentage Logic
        PercentageText = Total <= 0 ? "0%" : $"{(double)Current / Total * 100:F2}%";

        // Count Logic [cite: 8]
        if (Total >= 1000)
        {
            CurrentCountText = $"{Current} \n/ {Total}";
        }
        else
        {
            CurrentCountText = $"{Current} / {Total}";
        }

        // Tooltip Logic
        ComputedTooltipText = $"{PercentageText}\n{Current} / {Total}";

        // 2. 更新高度逻辑 (原 UserControl 代码逻辑移植 [cite: 3])
        if (indicator != null)
        {
            // 注意：在 Customcontrol 中，我们通常基于控件本身的 ActualHeight 计算
            // 原代码基于 ProgressContainer，这里假设模板根元素就是容器
            double containerHeight = ActualHeight;

            // 考虑到 BorderThickness (2)，为了精确可以减去边框宽度，或者在 Template 中处理
            // 这里为了简单直接使用 ActualHeight，因为 TemplateBinding 通常会自动处理 Padding

            double percentage = Total <= 0 ? 0 : (double)Current / Total;
            // 限制最大高度不超过 100%
            if (percentage > 1) percentage = 1;

            indicator.Height = percentage * containerHeight;
        }
    }

    #endregion Logic
}
