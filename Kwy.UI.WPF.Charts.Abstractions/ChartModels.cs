using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.Abstractions;

public sealed class PieSliceData
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public double Value { get; set; }

    public Color Color { get; set; }

    public bool IsExploded { get; set; }
}

public readonly record struct PlotPoint(double X, double Y);

public interface IChartPlot
{
    bool IsActive { get; set; }

    void ClearData();
}
