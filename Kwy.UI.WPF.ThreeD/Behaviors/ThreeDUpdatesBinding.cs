using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using Kwy.UI.ThreeD.Abstractions.Updates;

namespace Kwy.UI.WPF.ThreeD.Behaviors;

/// <summary>
/// 将 ViewModel 提供的 3D 更新消息转发给门面控件。
/// </summary>
public static class ThreeDUpdatesBinding
{
    public static readonly DependencyProperty UpdateProperty = DependencyProperty.RegisterAttached(
        "Update",
        typeof(ThreeDSceneUpdate),
        typeof(ThreeDUpdatesBinding),
        new PropertyMetadata(null, OnUpdateChanged));

    public static readonly DependencyProperty UpdatesProperty = DependencyProperty.RegisterAttached(
        "Updates",
        typeof(IEnumerable),
        typeof(ThreeDUpdatesBinding),
        new PropertyMetadata(null, OnUpdatesChanged));

    private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription",
        typeof(CollectionSubscription),
        typeof(ThreeDUpdatesBinding));

    public static void SetUpdate(DependencyObject element, ThreeDSceneUpdate? value) =>
        element.SetValue(UpdateProperty, value);

    public static ThreeDSceneUpdate? GetUpdate(DependencyObject element) =>
        (ThreeDSceneUpdate?)element.GetValue(UpdateProperty);

    public static void SetUpdates(DependencyObject element, IEnumerable? value) =>
        element.SetValue(UpdatesProperty, value);

    public static IEnumerable? GetUpdates(DependencyObject element) =>
        (IEnumerable?)element.GetValue(UpdatesProperty);

    public static bool TryPush(DependencyObject element, ThreeDSceneUpdate update)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(update);

        if (element is not IThreeDUpdateSink sink)
            return false;

        sink.Apply(update);
        return true;
    }

    public static bool TryReset(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is not IThreeDUpdateSink sink)
            return false;

        sink.Reset();
        return true;
    }

    public static bool CanAccept(DependencyObject element) => element is IThreeDUpdateSink;

    private static void OnUpdateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is ThreeDSceneUpdate update)
            TryPush(sender, update);
    }

    private static void OnUpdatesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender.GetValue(SubscriptionProperty) is CollectionSubscription previous)
            previous.Dispose();

        sender.ClearValue(SubscriptionProperty);
        if (args.NewValue is INotifyCollectionChanged observable)
            sender.SetValue(SubscriptionProperty, new CollectionSubscription(sender, observable));

        Replay(sender);
    }

    private static void Replay(DependencyObject target)
    {
        if (target is not IThreeDUpdateSink sink)
            return;

        sink.Reset();
        foreach (var update in GetUpdates(target)?.OfType<ThreeDSceneUpdate>() ?? [])
            sink.Apply(update);
    }

    private sealed class CollectionSubscription : IWeakEventListener, IDisposable
    {
        private readonly WeakReference<DependencyObject> target;
        private INotifyCollectionChanged? source;

        public CollectionSubscription(DependencyObject target, INotifyCollectionChanged source)
        {
            this.target = new WeakReference<DependencyObject>(target);
            this.source = source;
            CollectionChangedEventManager.AddListener(source, this);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs args)
        {
            if (managerType != typeof(CollectionChangedEventManager) ||
                args is not NotifyCollectionChangedEventArgs change ||
                !target.TryGetTarget(out var element))
                return false;

            if (change.Action == NotifyCollectionChangedAction.Add &&
                element is IThreeDUpdateSink sink &&
                change.NewItems is not null)
            {
                foreach (var update in change.NewItems.OfType<ThreeDSceneUpdate>())
                    sink.Apply(update);
            }
            else if (change.Action != NotifyCollectionChangedAction.Move)
            {
                Replay(element);
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
    }
}
