using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.Primitives;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 时间线图门面控件。
/// </summary>
public sealed class KwyTimelinePlotView : KwyChartView
{
    public static readonly DependencyProperty TitleProperty = Register<string>(nameof(Title), string.Empty);
    public static readonly DependencyProperty SubtitleProperty = Register<string>(nameof(Subtitle), string.Empty);
    public static readonly DependencyProperty TimeAxisTitleProperty = Register<string>(nameof(TimeAxisTitle), "Time (ms)");
    public static readonly DependencyProperty ProcessAxisTitleProperty = Register<string>(nameof(ProcessAxisTitle), "Process");
    public static readonly DependencyProperty ViewWindowProperty = Register(nameof(ViewWindow), 30000d);
    public static readonly DependencyProperty ChannelsProperty = Register<IEnumerable<ChartChannelDefinition>?>(nameof(Channels), null);

    public KwyTimelinePlotView() => RefreshDefinition();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string TimeAxisTitle { get => (string)GetValue(TimeAxisTitleProperty); set => SetValue(TimeAxisTitleProperty, value); }
    public string ProcessAxisTitle { get => (string)GetValue(ProcessAxisTitleProperty); set => SetValue(ProcessAxisTitleProperty, value); }
    public double ViewWindow { get => (double)GetValue(ViewWindowProperty); set => SetValue(ViewWindowProperty, value); }
    public IEnumerable<ChartChannelDefinition>? Channels { get => (IEnumerable<ChartChannelDefinition>?)GetValue(ChannelsProperty); set => SetValue(ChannelsProperty, value); }

    public void AddChannel(string key, string name, Rgba32? color = null)
    {
        var channels = Channels?.Where(channel => channel.Key != key).ToList() ?? [];
        channels.Add(new ChartChannelDefinition(key, name, color));
        SetCurrentValue(ChannelsProperty, channels);
    }

    public void StartProcess(string channelKey, string label = "") =>
        Apply(new StartTimelineProcess(channelKey, label));

    public void StopProcess(string channelKey) => Apply(new StopTimelineProcess(channelKey));

    public void ClearData() => Apply(ClearChartData.Instance);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(KwyTimelinePlotView), new PropertyMetadata(defaultValue, OnDefinitionPropertyChanged));

    private static void OnDefinitionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyTimelinePlotView)sender).RefreshDefinition();

    private void RefreshDefinition()
    {
        Definition = new TimelineChartDefinition
        {
            Title = Title,
            Subtitle = Subtitle,
            Channels = Channels?.ToArray() ?? [],
            Options = new TimelineChartOptions
            {
                Title = Title,
                Subtitle = Subtitle,
                TimeAxisTitle = TimeAxisTitle,
                ProcessAxisTitle = ProcessAxisTitle,
                ViewWindow = ViewWindow
            }
        };
    }
}
