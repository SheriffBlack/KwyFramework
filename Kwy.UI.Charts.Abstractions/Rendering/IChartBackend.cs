using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Updates;

namespace Kwy.UI.Charts.Abstractions.Rendering;

[Flags]
public enum ChartFeatures
{
    None = 0,
    Waveform = 1 << 0,
    ScatterTrend = 1 << 1,
    Histogram = 1 << 2,
    Pie = 1 << 3,
    Timeline = 1 << 4,
    Export = 1 << 5
}

public sealed record ChartCapabilities(ChartFeatures Features)
{
    public bool Supports(ChartFeatures feature) => (Features & feature) == feature;
}

public interface IChartBackend
{
    string Name { get; }

    ChartCapabilities Capabilities { get; }
}

public interface IChartBackend<TView> : IChartBackend
    where TView : class
{
    IChartSession<TView> Create(ChartDefinition definition);
}

public interface IChartSession : IDisposable
{
    ChartDefinition Definition { get; }

    bool IsActive { get; set; }

    void Apply(ChartUpdate update);
}

public interface IChartSession<out TView> : IChartSession
    where TView : class
{
    TView View { get; }
}
