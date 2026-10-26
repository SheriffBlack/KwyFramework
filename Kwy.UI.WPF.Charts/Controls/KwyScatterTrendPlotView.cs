using Kwy.UI.Charts.Abstractions.Updates;

namespace Kwy.UI.WPF.Charts.Controls;

/// <summary>
/// 散点趋势图门面控件。
/// </summary>
public sealed class KwyScatterTrendPlotView : KwyScatterTrendChart
{
    public void AddValue(double value, bool? isPass = null) =>
        Apply(new AppendChartValue("Default", value, isPass));

    public void SetLimits(double? lower, double? upper, double? target) =>
        Apply(new SetChartLimits(lower, upper, target));

    public void ClearData() => Apply(ClearChartData.Instance);
}
