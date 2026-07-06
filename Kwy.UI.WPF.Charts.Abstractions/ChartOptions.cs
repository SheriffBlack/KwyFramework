using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.Abstractions;

public class ChartOptions
{
    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public Color? Background { get; set; }
}

public sealed class WaveformChartOptions : ChartOptions
{
    public double TimeWindow { get; set; } = 20000;

    public int MaxPointsPerSeries { get; set; } = 100000;

    public int MaxPointsPerFrame { get; set; } = 20000;

    public string TimeAxisTitle { get; set; } = "Time (ms)";

    public string ValueAxisTitle { get; set; } = "Value";

    public string TimeAxisStringFormat { get; set; } = "0";

    public PlotRefreshMode RefreshMode { get; set; } = PlotRefreshMode.Smooth;

    public bool IsStacked { get; set; } = true;
}

public sealed class HistogramChartOptions : ChartOptions
{
    public PlotOrientation Orientation { get; set; } = PlotOrientation.Vertical;

    public double MinBinWidth { get; set; } = 0.01;

    public int MaxValuesPerFrame { get; set; } = 20000;

    public string ValueAxisTitle { get; set; } = "Value";

    public string FrequencyAxisTitle { get; set; } = "Frequency";
}

public sealed class ScatterTrendChartOptions : ChartOptions
{
    public PlotOrientation Orientation { get; set; } = PlotOrientation.Horizontal;

    public int ViewWindow { get; set; } = 1000;

    public int MaxValuesPerFrame { get; set; } = 20000;

    public string SampleAxisTitle { get; set; } = "Sample";

    public string ValueAxisTitle { get; set; } = "Value";
}

public sealed class PieChartOptions : ChartOptions
{
    public double InnerDiameter { get; set; } = 0.4;
}

public sealed class TimelineChartOptions : ChartOptions
{
    public double ViewWindow { get; set; } = 30000;

    public string TimeAxisTitle { get; set; } = "Time (ms)";

    public string ProcessAxisTitle { get; set; } = "Process";
}
