using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.Primitives;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 饼图门面控件。
/// </summary>
public sealed class KwyPiePlotView : KwyChartView
{
    public static readonly DependencyProperty TitleProperty = Register<string>(nameof(Title), string.Empty);
    public static readonly DependencyProperty SubtitleProperty = Register<string>(nameof(Subtitle), string.Empty);
    public static readonly DependencyProperty InnerDiameterProperty = Register(nameof(InnerDiameter), 0.4d);

    public KwyPiePlotView() => RefreshDefinition();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public double InnerDiameter { get => (double)GetValue(InnerDiameterProperty); set => SetValue(InnerDiameterProperty, value); }

    public void AddOrUpdateSlice(
        string key,
        string label,
        double value,
        Rgba32? color = null,
        bool isExploded = false) =>
        Apply(new SetPieSlice(key, label, value, color, isExploded));

    public void ClearData() => Apply(ClearChartData.Instance);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(KwyPiePlotView), new PropertyMetadata(defaultValue, OnDefinitionPropertyChanged));

    private static void OnDefinitionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyPiePlotView)sender).RefreshDefinition();

    private void RefreshDefinition()
    {
        Definition = new PieChartDefinition
        {
            Title = Title,
            Subtitle = Subtitle,
            Options = new PieChartOptions
            {
                Title = Title,
                Subtitle = Subtitle,
                InnerDiameter = InnerDiameter
            }
        };
    }
}
