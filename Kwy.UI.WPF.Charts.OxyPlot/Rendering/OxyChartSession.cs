using System.Windows;
using Kwy.UI.Charts.Abstractions;
using Kwy.UI.Charts.Abstractions.Definitions;
using Kwy.UI.Charts.Abstractions.Rendering;
using Kwy.UI.Charts.Abstractions.Updates;

namespace Kwy.UI.WPF.Charts.OxyPlot.Rendering;

internal sealed class OxyChartSession(
    ChartDefinition definition,
    FrameworkElement view,
    IChartPlot chart,
    Action<ChartUpdate> apply) : IChartSession<FrameworkElement>
{
    private bool disposed;

    public FrameworkElement View { get; } = view;

    public ChartDefinition Definition { get; } = definition;

    public bool IsActive
    {
        get => chart.IsActive;
        set
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            chart.IsActive = value;
        }
    }

    public void Apply(ChartUpdate update)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(update);
        apply(update);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        chart.IsActive = false;
        if (chart is IDisposable disposable)
            disposable.Dispose();
    }
}
