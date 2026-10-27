using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.ThreeD.Abstractions.State;
using Kwy.UI.ThreeD.Abstractions.Updates;

namespace Kwy.UI.WPF.ThreeD.Behaviors;

/// <summary>
/// 将常见的节点、灯光和节点状态转换为通用 3D 场景更新。
/// </summary>
public static class ThreeDSceneBinding
{
    public static readonly DependencyProperty NodesProperty = DependencyProperty.RegisterAttached(
        "Nodes",
        typeof(IEnumerable<SceneNodeDescriptor>),
        typeof(ThreeDSceneBinding),
        new PropertyMetadata(null, OnNodesChanged));

    public static readonly DependencyProperty LightsProperty = DependencyProperty.RegisterAttached(
        "Lights",
        typeof(IEnumerable<LightDescriptor>),
        typeof(ThreeDSceneBinding),
        new PropertyMetadata(null, OnLightsChanged));

    public static readonly DependencyProperty NodeStateProperty = DependencyProperty.RegisterAttached(
        "NodeState",
        typeof(ThreeDNodeState),
        typeof(ThreeDSceneBinding),
        new PropertyMetadata(null, OnNodeStateChanged));

    public static readonly DependencyProperty NodeStatesProperty = DependencyProperty.RegisterAttached(
        "NodeStates",
        typeof(IEnumerable<ThreeDNodeState>),
        typeof(ThreeDSceneBinding),
        new PropertyMetadata(null, OnNodeStatesChanged));

    public static readonly DependencyProperty ResetKeyProperty = DependencyProperty.RegisterAttached(
        "ResetKey",
        typeof(object),
        typeof(ThreeDSceneBinding),
        new PropertyMetadata(null, OnResetKeyChanged));

    private static readonly DependencyProperty NodesSubscriptionProperty = RegisterSubscription("NodesSubscription");
    private static readonly DependencyProperty LightsSubscriptionProperty = RegisterSubscription("LightsSubscription");
    private static readonly DependencyProperty StatesSubscriptionProperty = RegisterSubscription("StatesSubscription");

    public static void SetNodes(DependencyObject element, IEnumerable<SceneNodeDescriptor>? value) =>
        element.SetValue(NodesProperty, value);

    public static IEnumerable<SceneNodeDescriptor>? GetNodes(DependencyObject element) =>
        (IEnumerable<SceneNodeDescriptor>?)element.GetValue(NodesProperty);

    public static void SetLights(DependencyObject element, IEnumerable<LightDescriptor>? value) =>
        element.SetValue(LightsProperty, value);

    public static IEnumerable<LightDescriptor>? GetLights(DependencyObject element) =>
        (IEnumerable<LightDescriptor>?)element.GetValue(LightsProperty);

    public static void SetNodeState(DependencyObject element, ThreeDNodeState? value) =>
        element.SetValue(NodeStateProperty, value);

    public static ThreeDNodeState? GetNodeState(DependencyObject element) =>
        (ThreeDNodeState?)element.GetValue(NodeStateProperty);

    public static void SetNodeStates(DependencyObject element, IEnumerable<ThreeDNodeState>? value) =>
        element.SetValue(NodeStatesProperty, value);

    public static IEnumerable<ThreeDNodeState>? GetNodeStates(DependencyObject element) =>
        (IEnumerable<ThreeDNodeState>?)element.GetValue(NodeStatesProperty);

    public static void SetResetKey(DependencyObject element, object? value) =>
        element.SetValue(ResetKeyProperty, value);

    public static object? GetResetKey(DependencyObject element) =>
        element.GetValue(ResetKeyProperty);

    private static DependencyProperty RegisterSubscription(string name) =>
        DependencyProperty.RegisterAttached(name, typeof(CollectionSubscription), typeof(ThreeDSceneBinding));

    private static void OnNodesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ReplaceSubscription(sender, NodesSubscriptionProperty, args.NewValue, CollectionKind.Nodes);
        Replay(sender);
    }

    private static void OnLightsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ReplaceSubscription(sender, LightsSubscriptionProperty, args.NewValue, CollectionKind.Lights);
        PushLights(sender);
    }

    private static void OnNodeStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is ThreeDNodeState state)
            PushState(sender, state);
    }

    private static void OnNodeStatesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ReplaceSubscription(sender, StatesSubscriptionProperty, args.NewValue, CollectionKind.States);
        Replay(sender);
    }

    private static void OnResetKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        Replay(sender);

    private static void ReplaceSubscription(
        DependencyObject target,
        DependencyProperty property,
        object? value,
        CollectionKind kind)
    {
        if (target.GetValue(property) is CollectionSubscription previous)
            previous.Dispose();

        target.ClearValue(property);
        if (value is INotifyCollectionChanged observable)
            target.SetValue(property, new CollectionSubscription(target, observable, kind));
    }

    private static void Replay(DependencyObject target)
    {
        if (!ThreeDUpdatesBinding.TryReset(target))
            return;

        foreach (var node in GetNodes(target) ?? [])
            ThreeDUpdatesBinding.TryPush(target, new AddSceneNode(node));

        PushLights(target);
        foreach (var state in GetNodeStates(target) ?? [])
            PushState(target, state);
    }

    private static void PushLights(DependencyObject target)
    {
        var lights = GetLights(target)?.ToArray();
        if (lights is not null)
            ThreeDUpdatesBinding.TryPush(target, new SetSceneLights(lights));
    }

    private static void PushState(DependencyObject target, ThreeDNodeState state)
    {
        if (state.Transform is { } transform)
            ThreeDUpdatesBinding.TryPush(target, new SetNodeTransform(state.NodeId, transform));
        if (state.IsVisible is { } isVisible)
            ThreeDUpdatesBinding.TryPush(target, new SetNodeVisibility(state.NodeId, isVisible));
        if (state.IsHitTestVisible is { } isHitTestVisible)
            ThreeDUpdatesBinding.TryPush(target, new SetNodeHitTestVisibility(state.NodeId, isHitTestVisible));
        if (state.Material is { } material)
            ThreeDUpdatesBinding.TryPush(target, new SetNodeMaterial(state.NodeId, material));
    }

    private enum CollectionKind
    {
        Nodes,
        Lights,
        States
    }

    private sealed class CollectionSubscription : IWeakEventListener, IDisposable
    {
        private readonly WeakReference<DependencyObject> target;
        private readonly CollectionKind kind;
        private INotifyCollectionChanged? source;

        public CollectionSubscription(
            DependencyObject target,
            INotifyCollectionChanged source,
            CollectionKind kind)
        {
            this.target = new WeakReference<DependencyObject>(target);
            this.source = source;
            this.kind = kind;
            CollectionChangedEventManager.AddListener(source, this);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs args)
        {
            if (managerType != typeof(CollectionChangedEventManager) ||
                args is not NotifyCollectionChangedEventArgs change ||
                !target.TryGetTarget(out var element))
                return false;

            switch (kind)
            {
                case CollectionKind.Nodes:
                    ApplyNodeChange(element, change);
                    break;
                case CollectionKind.Lights:
                    PushLights(element);
                    break;
                case CollectionKind.States:
                    ApplyStateChange(element, change);
                    break;
            }

            return true;
        }

        public void Dispose()
        {
            if (source is null)
                return;

            CollectionChangedEventManager.RemoveListener(source, this);
            source = null;
        }

        private static void ApplyNodeChange(DependencyObject target, NotifyCollectionChangedEventArgs change)
        {
            if (change.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace)
            {
                foreach (var node in change.OldItems?.OfType<SceneNodeDescriptor>() ?? [])
                    ThreeDUpdatesBinding.TryPush(target, new RemoveSceneNode(node.Id));
            }

            if (change.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace)
            {
                foreach (var node in change.NewItems?.OfType<SceneNodeDescriptor>() ?? [])
                    ThreeDUpdatesBinding.TryPush(target, new AddSceneNode(node));
                return;
            }

            if (change.Action == NotifyCollectionChangedAction.Reset)
                Replay(target);
        }

        private static void ApplyStateChange(DependencyObject target, NotifyCollectionChangedEventArgs change)
        {
            if (change.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var state in change.NewItems?.OfType<ThreeDNodeState>() ?? [])
                    PushState(target, state);
            }
            else if (change.Action != NotifyCollectionChangedAction.Move)
            {
                Replay(target);
            }
        }
    }
}
