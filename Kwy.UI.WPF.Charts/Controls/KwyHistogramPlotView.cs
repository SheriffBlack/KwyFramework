using Kwy.UI.Charts.Abstractions.Updates;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 直方图门面控件。
/// </summary>
public sealed class KwyHistogramPlotView : KwyHistogramChart
{
    public void AddValue(double value) => Apply(new AppendChartValue("Default", value));

    public void AddValues(IEnumerable<double> values) =>
        Apply(new AppendChartValues("Default", values.ToArray()));

    public void SetLimits(double? lower, double? upper, double? target) =>
        Apply(new SetChartLimits(lower, upper, target));

    public void ClearData() => Apply(ClearChartData.Instance);
}
