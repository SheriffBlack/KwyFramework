using System.Windows;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Rendering;
using Kwy.UI.Charts.Abstractions.Updates;
using OxyPlot;
using OxyPlot.Wpf;

namespace Kwy.UI.WPF.Charts.OxyPlot.Rendering;

public sealed class OxyChartBackend : IChartBackend<FrameworkElement>
{
    public string Name => "OxyPlot";

    public ChartCapabilities Capabilities { get; } = new(
        ChartFeatures.Waveform |
        ChartFeatures.ScatterTrend |
        ChartFeatures.Histogram |
        ChartFeatures.Pie |
        ChartFeatures.Timeline);

    public IChartSession<FrameworkElement> Create(ChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition switch
        {
            WaveformChartDefinition waveform => CreateWaveform(waveform),
            ScatterTrendChartDefinition scatter => CreateScatter(scatter),
            HistogramChartDefinition histogram => CreateHistogram(histogram),
            PieChartDefinition pie => CreatePie(pie),
            TimelineChartDefinition timeline => CreateTimeline(timeline),
            _ => throw new NotSupportedException($"Chart definition '{definition.GetType().Name}' is not supported by OxyPlot.")
        };
    }

    private static OxyChartSession CreateWaveform(WaveformChartDefinition definition)
    {
        var options = definition.Options;
        var chart = new WaveformPlot(options)
        {
            Title = ResolveTitle(definition.Title, options.Title)
        };
        foreach (var channel in definition.Channels)
            chart.AddChannel(channel.Key, channel.Name, channel.Color, channel.IsDigital, channel.Minimum, channel.Maximum);

        var view = CreateView(chart.Model, chart.Controller);

        return new OxyChartSession(definition, view, chart, update =>
        {
            switch (update)
            {
                case AppendChartValue value:
                    chart.AddPoint(value.ChannelKey, value.Value);
                    break;
                case AppendChartValues values:
                    chart.AddPoints(values.ChannelKey, values.Values);
                    break;
                case ClearChartData:
                    chart.ClearData();
                    break;
            }
        });
    }

    private static OxyChartSession CreateScatter(ScatterTrendChartDefinition definition)
    {
        var options = definition.Options;
        var chart = new ScatterTrendPlot(options)
        {
            Title = ResolveTitle(definition.Title, options.Title)
        };
        chart.AddChannel(definition.Channel.Name, definition.Channel.Color);
        var view = CreateView(chart.Model, chart.Controller);

        return new OxyChartSession(definition, view, chart, update =>
        {
            switch (update)
            {
                case AppendChartValue value:
                    chart.AddValue(value.Value, value.IsPass);
                    break;
                case AppendChartValues values:
                    foreach (var value in values.Values)
                        chart.AddValue(value);
                    break;
                case SetChartLimits limits:
                    chart.SetLimits(limits.Lower, limits.Upper, limits.Target);
                    break;
                case SetChartLabels labels:
                    chart.LowerLimitLabel = labels.LowerLimit;
                    chart.UpperLimitLabel = labels.UpperLimit;
                    chart.TargetValueLabel = labels.Target;
                    chart.RefreshLimitLabels();
                    break;
                case ClearChartData:
                    chart.ClearData();
                    break;
            }
        });
    }

    private static OxyChartSession CreateHistogram(HistogramChartDefinition definition)
    {
        var options = definition.Options;
        var chart = new HistogramPlot(options)
        {
            Title = ResolveTitle(definition.Title, options.Title)
        };
        chart.AddChannel(definition.Channel.Name, definition.Channel.Color);
        var view = CreateView(chart.Model, chart.Controller);

        return new OxyChartSession(definition, view, chart, update =>
        {
            switch (update)
            {
                case AppendChartValue value:
                    chart.AddValue(value.Value);
                    break;
                case AppendChartValues values:
                    chart.AddValues(values.Values);
                    break;
                case SetChartLimits limits:
                    chart.SetLimits(limits.Lower, limits.Upper, limits.Target);
                    break;
                case SetChartLabels labels:
                    chart.LowerLimitLabel = labels.LowerLimit;
                    chart.UpperLimitLabel = labels.UpperLimit;
                    chart.TargetValueLabel = labels.Target;
                    chart.RefreshLimitLabels();
                    break;
                case ClearChartData:
                    chart.ClearData();
                    break;
            }
        });
    }

    private static OxyChartSession CreatePie(PieChartDefinition definition)
    {
        var chart = new PiePlot(definition.Options)
        {
            Title = ResolveTitle(definition.Title, definition.Options.Title)
        };
        var view = CreateView(chart.Model, chart.Controller);
        return new OxyChartSession(definition, view, chart, update =>
        {
            switch (update)
            {
                case SetPieSlice slice:
                    chart.AddOrUpdateSlice(slice.Key, slice.Label, slice.Value, slice.Color, slice.IsExploded);
                    break;
                case ClearChartData:
                    chart.ClearData();
                    break;
            }
        });
    }

    private static OxyChartSession CreateTimeline(TimelineChartDefinition definition)
    {
        var options = definition.Options;
        var chart = new TimelinePlot(options)
        {
            Title = ResolveTitle(definition.Title, options.Title)
        };
        foreach (var channel in definition.Channels)
            chart.AddChannel(channel.Key, channel.Name, channel.Color);

        var view = CreateView(chart.Model, chart.Controller);

        return new OxyChartSession(definition, view, chart, update =>
        {
            switch (update)
            {
                case StartTimelineProcess start:
                    chart.StartProcess(start.ChannelKey, start.Label);
                    break;
                case StopTimelineProcess stop:
                    chart.StopProcess(stop.ChannelKey);
                    break;
                case ClearChartData:
                    chart.ClearData();
                    break;
            }
        });
    }

    private static PlotView CreateView(PlotModel model, IPlotController controller) => new()
    {
        Model = model,
        Controller = controller
    };

    private static string ResolveTitle(string definitionTitle, string optionsTitle) =>
        string.IsNullOrWhiteSpace(definitionTitle) ? optionsTitle : definitionTitle;
}
