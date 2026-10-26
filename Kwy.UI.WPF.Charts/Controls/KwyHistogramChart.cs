using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 用于直方图的与渲染器无关的WPF封装。
/// </summary>
public class KwyHistogramChart : KwyChartView
{
    public static readonly DependencyProperty TitleProperty = Register<string>(nameof(Title), string.Empty);
    public static readonly DependencyProperty SubtitleProperty = Register<string>(nameof(Subtitle), string.Empty);
    public static readonly DependencyProperty ValueAxisTitleProperty = Register<string>(nameof(ValueAxisTitle), "Value");
    public static readonly DependencyProperty FrequencyAxisTitleProperty = Register<string>(nameof(FrequencyAxisTitle), "Frequency");
    public static readonly DependencyProperty MinBinWidthProperty = Register(nameof(MinBinWidth), 0.01d);
    public static readonly DependencyProperty OrientationProperty = Register(nameof(Orientation), PlotOrientation.Vertical);
    public static readonly DependencyProperty MaxValuesPerFrameProperty = Register(nameof(MaxValuesPerFrame), 20000);

    public KwyHistogramChart() => RefreshDefinition();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string ValueAxisTitle { get => (string)GetValue(ValueAxisTitleProperty); set => SetValue(ValueAxisTitleProperty, value); }
    public string FrequencyAxisTitle { get => (string)GetValue(FrequencyAxisTitleProperty); set => SetValue(FrequencyAxisTitleProperty, value); }
    public double MinBinWidth { get => (double)GetValue(MinBinWidthProperty); set => SetValue(MinBinWidthProperty, value); }
    public PlotOrientation Orientation { get => (PlotOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public int MaxValuesPerFrame { get => (int)GetValue(MaxValuesPerFrameProperty); set => SetValue(MaxValuesPerFrameProperty, value); }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(KwyHistogramChart), new PropertyMetadata(defaultValue, OnDefinitionPropertyChanged));

    private static void OnDefinitionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyHistogramChart)sender).RefreshDefinition();

    private void RefreshDefinition()
    {
        Definition = new HistogramChartDefinition
        {
            Title = Title,
            Subtitle = Subtitle,
            Channel = new ChartChannelDefinition("Default", string.IsNullOrWhiteSpace(Title) ? "Default" : Title),
            Options = new HistogramChartOptions
            {
                Title = Title,
                Subtitle = Subtitle,
                Orientation = Orientation,
                ValueAxisTitle = ValueAxisTitle,
                FrequencyAxisTitle = FrequencyAxisTitle,
                MinBinWidth = MinBinWidth,
                MaxValuesPerFrame = MaxValuesPerFrame
            }
        };
    }
}
