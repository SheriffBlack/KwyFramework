using System.Windows;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.Charts.Abstractions.Values;
using Kwy.UI.WPF.Charts.Behaviors;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class ChartValuesBindingTests
{
    [Fact]
    public void ResetKey_ReplaysLimitsLabelsAndHistoricalPoints()
    {
        var target = new UpdateTarget();
        ChartValuesBinding.SetChannelKey(target, "Temperature");
        ChartValuesBinding.SetPoints(target,
        [
            new ChartValuePoint(1, 10D, true),
            new ChartValuePoint(2, 11D, false)
        ]);
        ChartValuesBinding.SetLimits(target, new ChartLimitRange(8D, 12D, 10D));
        ChartValuesBinding.SetLowerLimitLabel(target, "下限");
        ChartValuesBinding.SetUpperLimitLabel(target, "上限");
        ChartValuesBinding.SetTargetValueLabel(target, "目标值");

        ChartValuesBinding.SetResetKey(target, "Parameter-A");

        Assert.Collection(
            target.Updates,
            update => Assert.IsType<ClearChartData>(update),
            update => Assert.Equal(new SetChartLimits(8D, 12D, 10D), update),
            update => Assert.Equal(new SetChartLabels("下限", "上限", "目标值"), update),
            update => Assert.Equal(new AppendChartValue("Temperature", 10D, true), update),
            update => Assert.Equal(new AppendChartValue("Temperature", 11D, false), update));
    }

    [Fact]
    public void Point_ForwardsIncrementAndNullClearsData()
    {
        var target = new UpdateTarget();
        var point = new ChartValuePoint(1, 42D, true, 100D);

        ChartValuesBinding.SetPoint(target, point);

        Assert.Equal([new AppendChartValue("Default", 42D, true, 100D)], target.Updates);

        ChartValuesBinding.SetPoint(target, null);

        Assert.Equal([ClearChartData.Instance], target.Updates);
    }

    [Fact]
    public void Point_IgnoresNonFiniteValues()
    {
        var target = new UpdateTarget();

        ChartValuesBinding.SetPoint(target, new ChartValuePoint(1, double.NaN));

        Assert.Empty(target.Updates);
    }

    private sealed class UpdateTarget : DependencyObject, IChartUpdateSink
    {
        public List<ChartUpdate> Updates { get; } = [];

        public void Apply(ChartUpdate update)
        {
            if (update is ClearChartData)
                Updates.Clear();

            Updates.Add(update);
        }
    }
}
