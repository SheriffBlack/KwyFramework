using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Communicate.Core;

/// <summary>
/// Common lifecycle, state, disposal, and single-flight reconnection behavior.
/// </summary>
public abstract class CommunicationClientBase : ICommunicationClient
{
    protected readonly IProtocolConfig config;
    protected bool disposed;

    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);
    private readonly object reconnectSync = new();
    private readonly object keepAliveSync = new();
    private CancellationTokenSource lifetimeCancellation = new();
    private CancellationTokenSource? reconnectCancellation;
    private CancellationTokenSource? keepAliveCancellation;
    private Task reconnectTask = Task.CompletedTask;
    private volatile int state = (int)ConnectionState.Disconnected;

    public ConnectionState State
    {
        get => (ConnectionState)state;
        protected set
        {
            var target = (int)value;
            var previous = Interlocked.Exchange(ref state, target);
            if (previous != target)
            {
                OnConnectionStateChanged((ConnectionState)previous, value);
            }
        }
    }


    public bool IsConnected => State == ConnectionState.Connected && IsConnectionAlive();

    public IProtocolConfig Config => config;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    protected CommunicationClientBase(IProtocolConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    protected abstract Task ConnectCoreAsync(CancellationToken cancellationToken);
    protected abstract Task DisconnectCoreAsync(CancellationToken cancellationToken);
    protected abstract bool IsConnectionAlive();

    protected virtual Task OnConnectedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task<bool> CheckConnectionAliveAsync(CancellationToken cancellationToken)
        => Task.FromResult(IsConnectionAlive());

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        config.ValidateAndThrow();

        Exception? connectError = null;
        await lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
                return;

            CancelReconnect();
            ResetLifetimeCancellation();
            State = ConnectionState.Connecting;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeCancellation.Token);
            try
            {
                await DisconnectCoreSafelyAsync(CancellationToken.None);
                await ConnectCoreAsync(linked.Token);
                await OnConnectedAsync(linked.Token);
                State = ConnectionState.Connected;
                StartKeepAlive();
                return;
            }
            catch (OperationCanceledException)
            {
                CancelKeepAlive();
                await DisconnectCoreSafelyAsync(CancellationToken.None);
                State = ConnectionState.Disconnected;
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                connectError = ex;
                CancelKeepAlive();
                await DisconnectCoreSafelyAsync(CancellationToken.None);
                State = ConnectionState.Error;
                OnErrorOccurred(ex, $"Connect failed: {ex.Message}");
            }
        }
        finally
        {
            lifecycleSemaphore.Release();
        }

        if (config.AutoReconnect && connectError != null)
        {
            _ = TriggerReconnectAsync();
        }

        if (connectError != null)
            throw connectError;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        CancelReconnect();
        CancelKeepAlive();
        lifetimeCancellation.Cancel();

        await lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (State == ConnectionState.Disconnected)
                return;

            State = ConnectionState.Disconnecting;
            await DisconnectCoreSafelyAsync(cancellationToken);
            State = ConnectionState.Disconnected;
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    protected Task TriggerReconnectAsync()
    {
        if (disposed || !config.AutoReconnect)
            return Task.CompletedTask;

        lock (reconnectSync)
        {
            if (!reconnectTask.IsCompleted)
                return reconnectTask;

            reconnectCancellation?.Dispose();
            reconnectCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
            reconnectTask = ReconnectLoopAsync(reconnectCancellation.Token);
            return reconnectTask;
        }
    }

    protected Task HandleCommunicationFailureAsync(Exception exception, string message)
    {
        CancelKeepAlive();
        OnErrorOccurred(exception, message);
        State = ConnectionState.Error;
        _ = TriggerReconnectAsync();
        return Task.CompletedTask;
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            State = ConnectionState.Reconnecting;
            var attempts = Math.Max(1, config.MaxReconnectAttempts);
            var baseDelay = Math.Max(0, config.ReconnectInterval);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1 && baseDelay > 0)
                {
                    var delayMs = Math.Min(baseDelay * Math.Pow(2, attempt - 2), int.MaxValue);
                    // 引入 0.9 ~ 1.1 的随机抖动
                    var jitter = Random.Shared.NextDouble() * 0.2 + 0.9;
                    var delay = TimeSpan.FromMilliseconds(delayMs * jitter);
                    await Task.Delay(delay, cancellationToken);
                }

                await lifecycleSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await DisconnectCoreSafelyAsync(CancellationToken.None);
                    await ConnectCoreAsync(cancellationToken);
                    await OnConnectedAsync(cancellationToken);
                    State = ConnectionState.Connected;
                    StartKeepAlive();
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    OnErrorOccurred(ex, $"Reconnect attempt {attempt} failed: {ex.Message}");
                    State = ConnectionState.Reconnecting;
                }
                finally
                {
                    lifecycleSemaphore.Release();
                }
            }

            State = ConnectionState.Error;
            OnErrorOccurred(new InvalidOperationException("Automatic reconnection failed."), "Automatic reconnection attempts exhausted.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DisconnectCoreSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await DisconnectCoreAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex, $"Disconnect failed: {ex.Message}");
        }
    }

    private void CancelReconnect()
    {
        lock (reconnectSync)
        {
            reconnectCancellation?.Cancel();
        }
    }

    private void StartKeepAlive()
    {
        if (config is not IKeepAliveConfig { KeepAlive: true } keepAliveConfig)
            return;

        lock (keepAliveSync)
        {
            keepAliveCancellation?.Cancel();
            keepAliveCancellation?.Dispose();
            keepAliveCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
            _ = KeepAliveLoopAsync(keepAliveConfig, keepAliveCancellation.Token);
        }
    }

    private async Task KeepAliveLoopAsync(IKeepAliveConfig keepAliveConfig, CancellationToken cancellationToken)
    {
        var interval = Math.Max(keepAliveConfig.KeepAliveInterval, 1000);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);

                if (State != ConnectionState.Connected)
                    continue;

                var alive = await CheckConnectionAliveAsync(cancellationToken);
                if (!alive)
                {
                    await HandleCommunicationFailureAsync(
                        new IOException("KeepAlive health check failed."),
                        "KeepAlive health check failed.");
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await HandleCommunicationFailureAsync(ex, $"KeepAlive health check failed: {ex.Message}");
        }
    }

    private void CancelKeepAlive()
    {
        lock (keepAliveSync)
        {
            keepAliveCancellation?.Cancel();
        }
    }

    private void ResetLifetimeCancellation()
    {
        if (!lifetimeCancellation.IsCancellationRequested)
            return;

        lifetimeCancellation.Dispose();
        lifetimeCancellation = new CancellationTokenSource();
    }

    protected void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    protected virtual void OnConnectionStateChanged(ConnectionState previousState, ConnectionState currentState)
        => ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previousState, currentState));

    protected virtual void OnErrorOccurred(Exception exception, string message)
        => ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(exception, message));

    public virtual async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await DisconnectAsync();
        disposed = true;
        CancelReconnect();
        CancelKeepAlive();
        reconnectCancellation?.Dispose();
        keepAliveCancellation?.Dispose();
        lifetimeCancellation.Dispose();
        lifecycleSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    public virtual void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
