using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.Primitives;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 波形图门面控件。
/// </summary>
public sealed class KwyWaveformPlotView : KwyChartView
{
    public static readonly DependencyProperty TitleProperty = Register<string>(nameof(Title), string.Empty);
    public static readonly DependencyProperty SubtitleProperty = Register<string>(nameof(Subtitle), string.Empty);
    public static readonly DependencyProperty TimeAxisTitleProperty = Register<string>(nameof(TimeAxisTitle), "Time (ms)");
    public static readonly DependencyProperty ValueAxisTitleProperty = Register<string>(nameof(ValueAxisTitle), "Value");
    public static readonly DependencyProperty TimeAxisStringFormatProperty = Register<string>(nameof(TimeAxisStringFormat), "0");
    public static readonly DependencyProperty TimeWindowProperty = Register(nameof(TimeWindow), 20000d);
    public static readonly DependencyProperty MaxPointsPerSeriesProperty = Register(nameof(MaxPointsPerSeries), 100000);
    public static readonly DependencyProperty MaxPointsPerFrameProperty = Register(nameof(MaxPointsPerFrame), 20000);
    public static readonly DependencyProperty RefreshModeProperty = Register(nameof(RefreshMode), PlotRefreshMode.Smooth);
    public static readonly DependencyProperty IsStackedProperty = Register(nameof(IsStacked), true);
    public static readonly DependencyProperty ChannelsProperty = Register<IEnumerable<ChartChannelDefinition>?>(nameof(Channels), null);

    public KwyWaveformPlotView() => RefreshDefinition();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string TimeAxisTitle { get => (string)GetValue(TimeAxisTitleProperty); set => SetValue(TimeAxisTitleProperty, value); }
    public string ValueAxisTitle { get => (string)GetValue(ValueAxisTitleProperty); set => SetValue(ValueAxisTitleProperty, value); }
    public string TimeAxisStringFormat { get => (string)GetValue(TimeAxisStringFormatProperty); set => SetValue(TimeAxisStringFormatProperty, value); }
    public double TimeWindow { get => (double)GetValue(TimeWindowProperty); set => SetValue(TimeWindowProperty, value); }
    public int MaxPointsPerSeries { get => (int)GetValue(MaxPointsPerSeriesProperty); set => SetValue(MaxPointsPerSeriesProperty, value); }
    public int MaxPointsPerFrame { get => (int)GetValue(MaxPointsPerFrameProperty); set => SetValue(MaxPointsPerFrameProperty, value); }
    public PlotRefreshMode RefreshMode { get => (PlotRefreshMode)GetValue(RefreshModeProperty); set => SetValue(RefreshModeProperty, value); }
    public bool IsStacked { get => (bool)GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }
    public IEnumerable<ChartChannelDefinition>? Channels { get => (IEnumerable<ChartChannelDefinition>?)GetValue(ChannelsProperty); set => SetValue(ChannelsProperty, value); }

    public void AddChannel(
        string key,
        string name,
        Rgba32? color = null,
        bool isDigital = false,
        double? minimum = null,
        double? maximum = null)
    {
        var channels = Channels?.Where(channel => channel.Key != key).ToList() ?? [];
        channels.Add(new ChartChannelDefinition(key, name, color, isDigital, minimum, maximum));
        SetCurrentValue(ChannelsProperty, channels);
    }

    public void AddPoint(string channelKey, double value) =>
        Apply(new AppendChartValue(channelKey, value));

    public void AddPoints(string channelKey, IEnumerable<double> values) =>
        Apply(new AppendChartValues(channelKey, values.ToArray()));

    public void ClearData() => Apply(ClearChartData.Instance);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(KwyWaveformPlotView), new PropertyMetadata(defaultValue, OnDefinitionPropertyChanged));

    private static void OnDefinitionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyWaveformPlotView)sender).RefreshDefinition();

    private void RefreshDefinition()
    {
        Definition = new WaveformChartDefinition
        {
            Title = Title,
            Subtitle = Subtitle,
            Channels = Channels?.ToArray() ?? [],
            Options = new WaveformChartOptions
            {
                Title = Title,
                Subtitle = Subtitle,
                TimeAxisTitle = TimeAxisTitle,
                ValueAxisTitle = ValueAxisTitle,
                TimeAxisStringFormat = TimeAxisStringFormat,
                TimeWindow = TimeWindow,
                MaxPointsPerSeries = MaxPointsPerSeries,
                MaxPointsPerFrame = MaxPointsPerFrame,
                RefreshMode = RefreshMode,
                IsStacked = IsStacked
            }
        };
    }
}
