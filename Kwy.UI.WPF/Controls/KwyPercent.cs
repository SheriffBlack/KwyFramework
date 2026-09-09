using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Displays a normalized percentage calculated from a current value and a total value.
/// Text, tooltip and indicator layout are provided by the control template.
/// </summary>
public class KwyPercent : Control
{
    static KwyPercent()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KwyPercent),
            new FrameworkPropertyMetadata(typeof(KwyPercent)));
    }

    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total),
        typeof(double),
        typeof(KwyPercent),
        new FrameworkPropertyMetadata(0d, OnValueChanged),
        IsFinite);

    public double Total
    {
        get => (double)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current),
        typeof(double),
        typeof(KwyPercent),
        new FrameworkPropertyMetadata(0d, OnValueChanged),
        IsFinite);

    public double Current
    {
        get => (double)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    private static readonly DependencyPropertyKey PercentagePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Percentage),
        typeof(double),
        typeof(KwyPercent),
        new FrameworkPropertyMetadata(0d));

    public static readonly DependencyProperty PercentageProperty = PercentagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the current percentage normalized to the range 0 through 1.
    /// </summary>
    public double Percentage => (double)GetValue(PercentageProperty);

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(KwyPercent));

    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty);
        set => SetValue(ShowPercentageProperty, value);
    }

    public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(
        nameof(ShowPercentage), typeof(bool), typeof(KwyPercent), new PropertyMetadata(true));

    public bool ShowCount
    {
        get => (bool)GetValue(ShowCountProperty);
        set => SetValue(ShowCountProperty, value);
    }

    public static readonly DependencyProperty ShowCountProperty = DependencyProperty.Register(
        nameof(ShowCount), typeof(bool), typeof(KwyPercent), new PropertyMetadata(true));

    public bool ShowToolTip
    {
        get => (bool)GetValue(ShowToolTipProperty);
        set => SetValue(ShowToolTipProperty, value);
    }

    public static readonly DependencyProperty ShowToolTipProperty = DependencyProperty.Register(
        nameof(ShowToolTip), typeof(bool), typeof(KwyPercent), new PropertyMetadata(true));

    public string PercentageStringFormat
    {
        get => (string)GetValue(PercentageStringFormatProperty);
        set => SetValue(PercentageStringFormatProperty, value);
    }

    public static readonly DependencyProperty PercentageStringFormatProperty = DependencyProperty.Register(
        nameof(PercentageStringFormat), typeof(string), typeof(KwyPercent), new PropertyMetadata("P2"));

    public string CountStringFormat
    {
        get => (string)GetValue(CountStringFormatProperty);
        set => SetValue(CountStringFormatProperty, value);
    }

    public static readonly DependencyProperty CountStringFormatProperty = DependencyProperty.Register(
        nameof(CountStringFormat), typeof(string), typeof(KwyPercent), new PropertyMetadata("{0} / {1}"));

    protected override AutomationPeer OnCreateAutomationPeer()
        => new KwyPercentAutomationPeer(this);

    private static bool IsFinite(object value)
        => value is double number && double.IsFinite(number);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var percent = (KwyPercent)d;
        double value = percent.Total <= 0d
            ? 0d
            : Math.Clamp(percent.Current / percent.Total, 0d, 1d);
        percent.SetValue(PercentagePropertyKey, value);
    }
}
