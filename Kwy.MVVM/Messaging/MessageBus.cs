using System.Collections.Concurrent;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;

namespace Kwy.MVVM.Messaging;

/// <summary>
/// Default message bus implementation based on CommunityToolkit WeakReferenceMessenger.
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly WeakReferenceMessenger messenger = new();
    private readonly ConcurrentDictionary<Type, object> latestMessages = new();
    private readonly IMessageDispatcher uiDispatcher;
    private readonly IReadOnlyList<IMessageBusObserver> observers;

    public MessageBus()
        : this(new InlineMessageDispatcher(), Array.Empty<IMessageBusObserver>())
    {
    }

    public MessageBus(IMessageDispatcher uiDispatcher)
        : this(uiDispatcher, Array.Empty<IMessageBusObserver>())
    {
    }

    public MessageBus(IMessageDispatcher uiDispatcher, IEnumerable<IMessageBusObserver> observers)
    {
        this.uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        this.observers = observers?.ToArray() ?? Array.Empty<IMessageBusObserver>();
    }

    public void Publish<TMessage>(TMessage message)
        where TMessage : class
    {
        Publish(message, MessagePublishOptions.Default);
    }

    public void Publish<TMessage>(TMessage message, MessagePublishOptions options)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        if (options.RetainLatest)
        {
            latestMessages[typeof(TMessage)] = message;
        }

        foreach (IMessageBusObserver observer in observers)
        {
            observer.OnPublished(typeof(TMessage), message);
        }

        messenger.Send(message);
    }

    public ValueTask PublishAsync<TMessage>(
        TMessage message,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        Publish(message, options ?? MessagePublishOptions.Default);
        return ValueTask.CompletedTask;
    }

    public IDisposable Subscribe<TMessage>(
        object recipient,
        Action<TMessage> handler)
        where TMessage : class
    {
        return Subscribe(recipient, handler, MessageSubscribeOptions<TMessage>.Default);
    }

    public IDisposable Subscribe<TMessage>(
        object recipient,
        Action<TMessage> handler,
        MessageSubscribeOptions<TMessage> options)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        var weakHandler = new WeakHandler<TMessage>(handler);
        return SubscribeCore(
            recipient,
            static (_, state, message) => ((WeakHandler<TMessage>)state).Invoke(message),
            weakHandler,
            options);
    }

    public IDisposable Subscribe<TRecipient, TMessage>(
        TRecipient recipient,
        Action<TRecipient, TMessage> handler)
        where TRecipient : class
        where TMessage : class
    {
        return Subscribe(recipient, handler, MessageSubscribeOptions<TMessage>.Default);
    }

    public IDisposable Subscribe<TRecipient, TMessage>(
        TRecipient recipient,
        Action<TRecipient, TMessage> handler,
        MessageSubscribeOptions<TMessage> options)
        where TRecipient : class
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        return SubscribeCore(
            recipient,
            static (target, state, message) => ((Action<TRecipient, TMessage>)state)((TRecipient)target, message),
            handler,
            options);
    }

    public void Unsubscribe<TMessage>(object recipient)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(recipient);
        messenger.Unregister<TMessage>(recipient);
    }

    public void Unsubscribe(object recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        messenger.UnregisterAll(recipient);
    }

    public bool TryGetLatest<TMessage>(out TMessage? message)
        where TMessage : class
    {
        if (latestMessages.TryGetValue(typeof(TMessage), out object? latest)
            && latest is TMessage typedMessage)
        {
            message = typedMessage;
            return true;
        }

        message = default;
        return false;
    }

    public void ClearLatest<TMessage>()
        where TMessage : class
    {
        latestMessages.TryRemove(typeof(TMessage), out _);
    }

    private IDisposable SubscribeCore<TMessage>(
        object recipient,
        Action<object, object, TMessage> invoker,
        object invokerState,
        MessageSubscribeOptions<TMessage> options)
        where TMessage : class
    {
        // WeakReferenceMessenger allows one registration per recipient/message pair.
        // Kwy keeps the latest subscription for that pair, which matches most ViewModel usage.
        messenger.Unregister<TMessage>(recipient);
        messenger.Register<TMessage>(recipient, (target, message) =>
        {
            if (options.Filter is not null && !options.Filter(message))
            {
                return;
            }

            Dispatch(target, message, options.Thread, () => invoker(target, invokerState, message));
        });

        if (options.ReplayLatest && TryGetLatest(out TMessage? latestMessage) && latestMessage is not null)
        {
            if (options.Filter is null || options.Filter(latestMessage))
            {
                Dispatch(recipient, latestMessage, options.Thread, () => invoker(recipient, invokerState, latestMessage));
            }
        }

        return new SubscriptionHandle(() => messenger.Unregister<TMessage>(recipient));
    }

    private void Dispatch<TMessage>(object recipient, TMessage message, MessageThread thread, Action invoke)
        where TMessage : class
    {
        switch (thread)
        {
            case MessageThread.UI:
                if (uiDispatcher.CheckAccess())
                {
                    Invoke(recipient, message, invoke, rethrow: true);
                }
                else
                {
                    uiDispatcher.Post(() => Invoke(recipient, message, invoke, rethrow: true));
                }

                break;

            case MessageThread.Background:
                _ = Task.Run(() => Invoke(recipient, message, invoke, rethrow: false));
                break;

            case MessageThread.Publisher:
            default:
                Invoke(recipient, message, invoke, rethrow: true);
                break;
        }
    }

    private void Invoke<TMessage>(object recipient, TMessage message, Action invoke, bool rethrow)
        where TMessage : class
    {
        try
        {
            invoke();
            foreach (IMessageBusObserver observer in observers)
            {
                observer.OnHandled(typeof(TMessage), recipient, message);
            }
        }
        catch (Exception ex)
        {
            Exception error = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
            foreach (IMessageBusObserver observer in observers)
            {
                observer.OnHandlerError(typeof(TMessage), recipient, message, error);
            }

            if (rethrow)
            {
                throw error;
            }
        }
    }

    private sealed class WeakHandler<TMessage>
        where TMessage : class
    {
        private readonly WeakReference? targetReference;
        private readonly MethodInfo method;

        public WeakHandler(Action<TMessage> handler)
        {
            targetReference = handler.Target is null ? null : new WeakReference(handler.Target);
            method = handler.Method;
        }

        public void Invoke(TMessage message)
        {
            object? target = targetReference?.Target;
            if (targetReference is not null && target is null)
            {
                return;
            }

            method.Invoke(target, new object?[] { message });
        }
    }

    private sealed class SubscriptionHandle : IDisposable
    {
        private Action? dispose;

        public SubscriptionHandle(Action dispose)
        {
            this.dispose = dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref dispose, null)?.Invoke();
        }
    }
}
