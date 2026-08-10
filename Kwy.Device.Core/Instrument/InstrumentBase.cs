using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Instrument;
using System.Text;

namespace Kwy.Device.Core.Instrument;

/// <summary>
/// Base class for byte-transport instruments. All command, query, trigger, and result operations are serialized.
/// </summary>
public abstract class InstrumentBase : DeviceBase, IInstrumentDevice
{
    private static readonly TimeSpan ProtocolDisposeTimeout = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim executionSemaphore = new(1, 1);

    protected readonly ICommunicationClient protocol;
    protected readonly IByteTransport transport;

    public IProtocolConfig ProtocolConfig { get; }

    protected InstrumentBase(string deviceId, string deviceName, ICommunicationClient protocol)
        : base(deviceId, deviceName)
    {
        this.protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        transport = protocol as IByteTransport
            ?? throw new ArgumentException("Instrument communication must support IByteTransport.", nameof(protocol));
        ProtocolConfig = protocol.Config;
        DeviceParameter = ConfigFactory.CreateConfigFor(DeviceModel);
        SubscribeProtocolEvents();
    }

    protected InstrumentBase(
        string deviceId,
        string deviceName,
        IProtocolConfig protocolConfig,
        ICommunicationFactory? factory = null)
        : base(deviceId, deviceName)
    {
        ArgumentNullException.ThrowIfNull(protocolConfig);
        ArgumentNullException.ThrowIfNull(factory);

        ProtocolConfig = protocolConfig;
        protocol = factory.CreateClient(protocolConfig);
        transport = protocol as IByteTransport
            ?? throw new InvalidOperationException("Instrument communication must support IByteTransport.");
        DeviceParameter = ConfigFactory.CreateConfigFor(DeviceModel);
        SubscribeProtocolEvents();
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            await protocol.ConnectAsync(cancellationToken);
        }
        finally
        {
            executionSemaphore.Release();
        }
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            await protocol.DisconnectAsync(cancellationToken);
        }
        finally
        {
            executionSemaphore.Release();
        }
    }

    protected override bool IsConnectionAlive() => protocol.IsConnected;

    public ValueTask WriteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteSerializedAsync(
            token => transport.WriteTextAsync(command, cancellationToken: token),
            cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        return ExecuteSerializedAsync(token => transport.WriteAsync(data, token), cancellationToken);
    }

    public async ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        try
        {
            return await ExecuteSerializedAsync(async token =>
            {
                await transport.WriteTextAsync(command, cancellationToken: token);
                return await ReadResponseCoreAsync(token);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationFailed(DeviceOperationKind.Read, "Query", $"Instrument query failed. Command={FormatCommand(command)}, Error={ex.Message}", ex);
            throw;
        }
    }

    public async ValueTask<string> ReadResponseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteSerializedAsync(ReadResponseCoreAsync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationFailed(DeviceOperationKind.Read, "ReadResponse", $"Instrument read response failed. Error={ex.Message}", ex);
            throw;
        }
    }

    public async ValueTask TriggerAsync(string command = "*TRG", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        try
        {
            await ExecuteSerializedAsync(
                token => transport.WriteTextAsync(command, cancellationToken: token),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationFailed(DeviceOperationKind.Trigger, "Trigger", $"Instrument trigger failed. Command={FormatCommand(command)}, Error={ex.Message}", ex);
            throw;
        }
    }

    public async Task<string> WaitAndReadTriggeredResultAsync(
        Func<CancellationToken, Task> waitForCompletionAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waitForCompletionAsync);
        try
        {
            return await ExecuteSerializedAsync(async token =>
            {
                await waitForCompletionAsync(token);
                return await ReadResponseCoreAsync(token);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationFailed(DeviceOperationKind.Read, "WaitAndReadTriggeredResult", $"Instrument triggered result read failed. Error={ex.Message}", ex);
            throw;
        }
    }

    public override async Task ApplyConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteSerializedAsync(async token =>
            {
                if (DeviceParameter == null)
                    throw new InvalidOperationException("Device configuration is not set.");
                if (!DeviceParameter.Validate())
                    throw new InvalidOperationException("Device configuration is invalid.");

                var command = JoinCommand();
                if (!string.IsNullOrWhiteSpace(command))
                    await transport.WriteTextAsync(command, cancellationToken: token);
            }, cancellationToken).ConfigureAwait(false);

            RaiseOperationOccurred(
                DeviceOperationKind.ParameterWrite,
                "ApplyConfig",
                true,
                $"Instrument parameter write succeeded. Device={DeviceName}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationFailed(DeviceOperationKind.ParameterWrite, "ApplyConfig", $"Instrument parameter write failed. Device={DeviceName}, Error={ex.Message}", ex);
            throw;
        }
    }

    public virtual string JoinCommand() => string.Empty;

    protected ICommunicationClient Protocol => protocol;
    protected IByteTransport Transport => transport;

    protected virtual string ParseResponse(ReadOnlySpan<byte> responseBytes)
        => Encoding.UTF8.GetString(responseBytes).Trim();

    private void RaiseOperationFailed(DeviceOperationKind kind, string operationName, string message, Exception exception)
        => RaiseOperationOccurred(kind, operationName, false, message, exception);

    private static string FormatCommand(string command)
        => command.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private async ValueTask<string> ReadResponseCoreAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var bytesReceived = await transport.ReadAsync(buffer, cancellationToken);
        return bytesReceived <= 0 ? string.Empty : ParseResponse(buffer.AsSpan(0, bytesReceived));
    }

    private async ValueTask ExecuteSerializedAsync(
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureConnected();
        await executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            executionSemaphore.Release();
        }
    }

    private async ValueTask<T> ExecuteSerializedAsync<T>(
        Func<CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureConnected();
        await executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            executionSemaphore.Release();
        }
    }

    private void EnsureConnected()
    {
        if (!protocol.IsConnected)
            throw new InvalidOperationException("Instrument is not connected.");
    }

    private void SubscribeProtocolEvents()
    {
        protocol.ConnectionStateChanged += OnProtocolStateChanged;
        protocol.ErrorOccurred += OnProtocolErrorOccurred;
    }

    private void UnsubscribeProtocolEvents()
    {
        protocol.ConnectionStateChanged -= OnProtocolStateChanged;
        protocol.ErrorOccurred -= OnProtocolErrorOccurred;
    }

    protected virtual void OnProtocolStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        => RaiseStateChanged(e.CurrentState);

    protected virtual void OnProtocolErrorOccurred(object? sender, ErrorOccurredEventArgs e)
        => RaiseErrorOccurred(e.Message, e.Exception);

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            UnsubscribeProtocolEvents();
            try
            {
                await protocol.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RaiseErrorOccurred($"Instrument protocol cleanup failed: {ex.Message}", ex);
            }

            try
            {
                executionSemaphore.Dispose();
            }
            catch
            {
            }
        }
    }
}


