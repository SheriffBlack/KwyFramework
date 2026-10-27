using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows;
using Kwy.UI.ThreeD.Abstractions.Primitives;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.ThreeD.Abstractions.State;
using Kwy.UI.ThreeD.Abstractions.Updates;
using Kwy.UI.WPF.ThreeD.Behaviors;
using Xunit;

namespace Kwy.UI.WPF.Tests;

public sealed class ThreeDSceneBindingTests
{
    [Fact]
    public void ResetKey_ReplaysNodesLightsAndStates()
    {
        var node = CreateLineNode();
        var light = new AmbientLightDescriptor(LinearColor.White);
        var transform = Matrix4x4.CreateTranslation(1F, 2F, 3F);
        var target = new UpdateTarget();

        ThreeDSceneBinding.SetNodes(target, [node]);
        ThreeDSceneBinding.SetLights(target, [light]);
        ThreeDSceneBinding.SetNodeStates(target,
        [
            new ThreeDNodeState(
                node.Id,
                Transform: transform,
                IsVisible: false,
                IsHitTestVisible: false)
        ]);

        ThreeDSceneBinding.SetResetKey(target, "Scene-A");

        Assert.Equal(3, target.ResetCount);
        Assert.Collection(
            target.Updates,
            update => Assert.Equal(new AddSceneNode(node), update),
            update => Assert.Equal([light], Assert.IsType<SetSceneLights>(update).Lights),
            update => Assert.Equal(new SetNodeTransform(node.Id, transform), update),
            update => Assert.Equal(new SetNodeVisibility(node.Id, false), update),
            update => Assert.Equal(new SetNodeHitTestVisibility(node.Id, false), update));
    }

    [Fact]
    public void NodeState_ForwardsIncrementalState()
    {
        var nodeId = Guid.NewGuid();
        var target = new UpdateTarget();

        ThreeDSceneBinding.SetNodeState(
            target,
            new ThreeDNodeState(nodeId, IsVisible: true, IsHitTestVisible: false));

        Assert.Equal(
        [
            new SetNodeVisibility(nodeId, true),
            new SetNodeHitTestVisibility(nodeId, false)
        ], target.Updates);
    }

    [Fact]
    public void Nodes_ForwardsObservableCollectionChanges()
    {
        var first = CreateLineNode();
        var second = CreateLineNode();
        var nodes = new ObservableCollection<SceneNodeDescriptor> { first };
        var target = new UpdateTarget();

        ThreeDSceneBinding.SetNodes(target, nodes);
        nodes.Add(second);
        nodes.Remove(first);

        Assert.Equal(
        [
            new AddSceneNode(first),
            new AddSceneNode(second),
            new RemoveSceneNode(first.Id)
        ], target.Updates);
    }

    private static LineNodeDescriptor CreateLineNode() => new()
    {
        Positions = new Vector3[] { Vector3.Zero, Vector3.One }
    };

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
