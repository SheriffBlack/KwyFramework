using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows;
using Kwy.UI.ThreeD.Abstractions.Updates;
using Kwy.UI.WPF.ThreeD.Behaviors;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class ThreeDUpdatesBindingTests
{
    [Fact]
    public void Updates_ReplaysSnapshotAndForwardsCollectionChanges()
    {
        var first = new SetNodeVisibility(Guid.NewGuid(), false);
        var second = new SetNodeTransform(Guid.NewGuid(), Matrix4x4.CreateTranslation(1F, 2F, 3F));
        var updates = new ObservableCollection<ThreeDSceneUpdate> { first };
        var target = new UpdateTarget();

        ThreeDUpdatesBinding.SetUpdates(target, updates);

        Assert.Equal(1, target.ResetCount);
        Assert.Equal([first], target.Updates);

        updates.Add(second);

        Assert.Equal(1, target.ResetCount);
        Assert.Equal([first, second], target.Updates);

        updates.Remove(first);

        Assert.Equal(2, target.ResetCount);
        Assert.Equal([second], target.Updates);
    }

    [Fact]
    public void Update_ForwardsLatestMessage()
    {
        var update = new RemoveSceneNode(Guid.NewGuid());
        var target = new UpdateTarget();

        ThreeDUpdatesBinding.SetUpdate(target, update);

        Assert.Equal([update], target.Updates);
    }

    private sealed class UpdateTarget : DependencyObject, IThreeDUpdateSink
    {
        public List<ThreeDSceneUpdate> Updates { get; } = [];

        public int ResetCount { get; private set; }

        public void Apply(ThreeDSceneUpdate update) => Updates.Add(update);

        public void Reset()
        {
            ResetCount++;
            Updates.Clear();
        }
    }
}
