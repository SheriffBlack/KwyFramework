using System.Collections.ObjectModel;
using System.Windows;
using Kwy.UI.Charts.Abstractions.Updates;
using Kwy.UI.WPF.Charts.Behaviors;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class ChartUpdatesBindingTests
{
    [Fact]
    public void Updates_ReplaysSnapshotAndForwardsCollectionChanges()
    {
        var first = new AppendChartValue("A", 1D);
        var second = new AppendChartValue("A", 2D);
        var updates = new ObservableCollection<ChartUpdate> { first };
        var target = new UpdateTarget();

        ChartUpdatesBinding.SetUpdates(target, updates);

        Assert.Equal([ClearChartData.Instance, first], target.Updates);

        updates.Add(second);

        Assert.Equal([ClearChartData.Instance, first, second], target.Updates);

        updates.Remove(first);

        Assert.Equal([ClearChartData.Instance, second], target.Updates);
    }

    [Fact]
    public void Update_ForwardsLatestMessage()
    {
        var update = new SetChartLimits(1D, 2D, 1.5D);
        var target = new UpdateTarget();

        ChartUpdatesBinding.SetUpdate(target, update);

        Assert.Equal([update], target.Updates);
    }

    [Fact]
    public void TryPush_ReturnsFalseForUnsupportedTarget()
    {
        Assert.False(ChartUpdatesBinding.TryPush(
            new DependencyObject(),
            ClearChartData.Instance));
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
