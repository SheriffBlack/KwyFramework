using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 用于散点图/趋势图的无渲染器依赖的WPF封装层。
/// </summary>
public class KwyScatterTrendChart : KwyChartView
{
    public static readonly DependencyProperty TitleProperty = Register<string>(nameof(Title), string.Empty);
    public static readonly DependencyProperty SubtitleProperty = Register<string>(nameof(Subtitle), string.Empty);
    public static readonly DependencyProperty SampleAxisTitleProperty = Register<string>(nameof(SampleAxisTitle), "Sample");
    public static readonly DependencyProperty ValueAxisTitleProperty = Register<string>(nameof(ValueAxisTitle), "Value");
    public static readonly DependencyProperty SampleAxisMajorStepProperty = Register(nameof(SampleAxisMajorStep), double.NaN);
    public static readonly DependencyProperty SampleAxisViewWindowProperty = Register(nameof(SampleAxisViewWindow), 1000);
    public static readonly DependencyProperty OrientationProperty = Register(nameof(Orientation), PlotOrientation.Horizontal);
    public static readonly DependencyProperty MaxValuesPerFrameProperty = Register(nameof(MaxValuesPerFrame), 20000);

    public KwyScatterTrendChart() => RefreshDefinition();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string SampleAxisTitle { get => (string)GetValue(SampleAxisTitleProperty); set => SetValue(SampleAxisTitleProperty, value); }
    public string ValueAxisTitle { get => (string)GetValue(ValueAxisTitleProperty); set => SetValue(ValueAxisTitleProperty, value); }
    public double SampleAxisMajorStep { get => (double)GetValue(SampleAxisMajorStepProperty); set => SetValue(SampleAxisMajorStepProperty, value); }
    public int SampleAxisViewWindow { get => (int)GetValue(SampleAxisViewWindowProperty); set => SetValue(SampleAxisViewWindowProperty, value); }
    public PlotOrientation Orientation { get => (PlotOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public int MaxValuesPerFrame { get => (int)GetValue(MaxValuesPerFrameProperty); set => SetValue(MaxValuesPerFrameProperty, value); }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(KwyScatterTrendChart), new PropertyMetadata(defaultValue, OnDefinitionPropertyChanged));

    private static void OnDefinitionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyScatterTrendChart)sender).RefreshDefinition();

    private void RefreshDefinition()
    {
        Definition = new ScatterTrendChartDefinition
        {
            Title = Title,
            Subtitle = Subtitle,
            Channel = new ChartChannelDefinition("Default", string.IsNullOrWhiteSpace(Title) ? "Default" : Title),
            Options = new ScatterTrendChartOptions
            {
                Title = Title,
                Subtitle = Subtitle,
                Orientation = Orientation,
                SampleAxisTitle = SampleAxisTitle,
                SampleAxisMajorStep = SampleAxisMajorStep,
                ViewWindow = SampleAxisViewWindow,
                ValueAxisTitle = ValueAxisTitle,
                MaxValuesPerFrame = MaxValuesPerFrame
            }
        };
    }
}
