using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;

namespace Kwy.Device.Core;

/// <summary>
/// Common device identity, lifecycle, state, and disposal behavior.
/// </summary>
public abstract class DeviceBase : IDevice
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);

    public string DeviceId { get; protected set; }
    public string DeviceName { get; protected set; }
    public bool IsConnected => State == ConnectionState.Connected && IsConnectionAlive();
    public ConnectionState State { get; protected set; }
    public IDeviceConfig DeviceParameter { get; set; }

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
    public event EventHandler<DeviceOperationEventArgs>? OperationOccurred;

    protected bool disposed;

    public abstract string DeviceModel { get; }

    protected DeviceBase(string deviceId, string deviceName)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        DeviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        State = ConnectionState.Disconnected;
        DeviceParameter = default!;
    }

    protected DeviceBase(string deviceId, string deviceName, IDeviceConfig config) : this(deviceId, deviceName)
    {
        DeviceParameter = config ?? throw new ArgumentNullException(nameof(config));
    }

    protected abstract Task ConnectCoreAsync(CancellationToken cancellationToken);
    protected abstract Task DisconnectCoreAsync(CancellationToken cancellationToken);
    protected abstract bool IsConnectionAlive();

    protected virtual Task OnConnectedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task OnDisconnectingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected async Task HandleDeviceFailureAsync(
        string message,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (State == ConnectionState.Disconnected)
            {
                return;
            }

            RaiseErrorOccurred(message, exception);
            RaiseStateChanged(ConnectionState.Error);
            await DisconnectCoreSafelyAsync(CancellationToken.None);
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
                return;

            RaiseStateChanged(ConnectionState.Connecting);
            await ConnectCoreAsync(cancellationToken);
            await OnConnectedAsync(cancellationToken);
            RaiseStateChanged(ConnectionState.Connected);
        }
        catch (OperationCanceledException)
        {
            await DisconnectCoreSafelyAsync(CancellationToken.None);
            RaiseStateChanged(ConnectionState.Disconnected);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseErrorOccurred($"Device connection failed: {ex.Message}", ex);
            await DisconnectCoreSafelyAsync(CancellationToken.None);
            RaiseStateChanged(ConnectionState.Error);
            throw;
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (State == ConnectionState.Disconnected)
                return;

            RaiseStateChanged(ConnectionState.Disconnecting);
            await OnDisconnectingAsync(cancellationToken);
            await DisconnectCoreAsync(cancellationToken);
            RaiseStateChanged(ConnectionState.Disconnected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseErrorOccurred($"Device disconnection failed: {ex.Message}", ex);
            RaiseStateChanged(ConnectionState.Error);
            throw;
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public virtual Task ApplyConfigAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected void RaiseStateChanged(ConnectionState newState)
    {
        if (State == newState)
            return;

        var oldState = State;

        State = newState;
        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
    }

    protected void RaiseErrorOccurred(string message, Exception? ex = null)
        => ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ex ?? new Exception(message), message));

    protected void RaiseOperationOccurred(
        DeviceOperationKind kind,
        string operationName,
        bool isSuccess,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null)
        => OperationOccurred?.Invoke(
            this,
            new DeviceOperationEventArgs(kind, operationName, isSuccess, message, exception, properties));

    private async Task DisconnectCoreSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await OnDisconnectingAsync(cancellationToken);
            await DisconnectCoreAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseErrorOccurred($"Device cleanup failed: {ex.Message}", ex);
        }
    }

    protected void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        bool lockTaken = false;
        try
        {
            using var shutdownCts = new CancellationTokenSource(ShutdownTimeout);
            try
            {
                await lifecycleSemaphore.WaitAsync(shutdownCts.Token).ConfigureAwait(false);
                lockTaken = true;
            }
            catch (OperationCanceledException)
            {
                RaiseErrorOccurred($"Device dispose timed out waiting for lifecycle operation after {ShutdownTimeout.TotalSeconds:0}s.");
                disposed = true;
                GC.SuppressFinalize(this);
                return;
            }

            try
            {
                if (State != ConnectionState.Disconnected)
                {
                    RaiseStateChanged(ConnectionState.Disconnecting);
                    await DisconnectCoreSafelyAsync(shutdownCts.Token).ConfigureAwait(false);
                    RaiseStateChanged(ConnectionState.Disconnected);
                }
            }
            finally
            {
                disposed = true;
                lifecycleSemaphore.Release();
            }
        }
        finally
        {
            if (lockTaken)
            {
                lifecycleSemaphore.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }

    public virtual void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();
}




