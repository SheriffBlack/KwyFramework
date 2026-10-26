namespace Kwy.UI.Charts.Abstractions.Definitions;

/// <summary>
/// 描述用于创建图表会话的低频配置。
/// </summary>
public abstract record ChartDefinition
{
    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;
}

public sealed record WaveformChartDefinition : ChartDefinition
{
    public WaveformChartOptions Options { get; init; } = new();

    public IReadOnlyList<ChartChannelDefinition> Channels { get; init; } = [];
}

public sealed record ScatterTrendChartDefinition : ChartDefinition
{
    public ScatterTrendChartOptions Options { get; init; } = new();

    public ChartChannelDefinition Channel { get; init; } = new("Default", "Default");
}

public sealed record HistogramChartDefinition : ChartDefinition
{
    public HistogramChartOptions Options { get; init; } = new();

    public ChartChannelDefinition Channel { get; init; } = new("Default", "Default");
}

public sealed record PieChartDefinition : ChartDefinition
{
    public PieChartOptions Options { get; init; } = new();
}

public sealed record TimelineChartDefinition : ChartDefinition
{
    public TimelineChartOptions Options { get; init; } = new();

    public IReadOnlyList<ChartChannelDefinition> Channels { get; init; } = [];
}

public sealed record ChartChannelDefinition(
    string Key,
    string Name,
    Kwy.UI.Primitives.Rgba32? Color = null,
    bool IsDigital = false,
    double? Minimum = null,
    double? Maximum = null);
