using Kwy.UI.Primitives;

namespace Kwy.UI.Charts.Abstractions.Updates;

public abstract record ChartUpdate;

public interface IChartUpdateSink
{
    void Apply(ChartUpdate update);
}

public sealed record AppendChartValue(
    string ChannelKey,
    double Value,
    bool? IsPass = null,
    double? Timestamp = null) : ChartUpdate;

public sealed record AppendChartValues(
    string ChannelKey,
    IReadOnlyList<double> Values) : ChartUpdate;

public sealed record SetChartLimits(
    double? Lower,
    double? Upper,
    double? Target) : ChartUpdate;

public sealed record SetChartLabels(
    string LowerLimit,
    string UpperLimit,
    string Target) : ChartUpdate;

public sealed record SetPieSlice(
    string Key,
    string Label,
    double Value,
    Rgba32? Color = null,
    bool IsExploded = false) : ChartUpdate;

public sealed record StartTimelineProcess(string ChannelKey, string Label = "") : ChartUpdate;

public sealed record StopTimelineProcess(string ChannelKey) : ChartUpdate;

public sealed record ClearChartData : ChartUpdate
{
    public static ClearChartData Instance { get; } = new();
}
