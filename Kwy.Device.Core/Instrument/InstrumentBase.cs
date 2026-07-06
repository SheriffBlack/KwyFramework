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

    public ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteSerializedAsync(async token =>
        {
            await transport.WriteTextAsync(command, cancellationToken: token);
            return await ReadResponseCoreAsync(token);
        }, cancellationToken);
    }

    public ValueTask<string> ReadResponseAsync(CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(ReadResponseCoreAsync, cancellationToken);

    public ValueTask TriggerAsync(string command = "*TRG", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteSerializedAsync(
            token => transport.WriteTextAsync(command, cancellationToken: token),
            cancellationToken);
    }

    public Task<string> WaitAndReadTriggeredResultAsync(
        Func<CancellationToken, Task> waitForCompletionAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waitForCompletionAsync);
        return ExecuteSerializedAsync(async token =>
        {
            await waitForCompletionAsync(token);
            return await ReadResponseCoreAsync(token);
        }, cancellationToken).AsTask();
    }

    public override Task ApplyConfigurationAsync(CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(async token =>
        {
            if (DeviceParameter == null)
                throw new InvalidOperationException("Device configuration is not set.");
            if (!DeviceParameter.Validate())
                throw new InvalidOperationException("Device configuration is invalid.");

            var command = JoinCommand();
            if (!string.IsNullOrWhiteSpace(command))
                await transport.WriteTextAsync(command, cancellationToken: token);
        }, cancellationToken).AsTask();

    public virtual string JoinCommand() => string.Empty;

    protected ICommunicationClient Protocol => protocol;
    protected IByteTransport Transport => transport;

    protected virtual string ParseResponse(ReadOnlySpan<byte> responseBytes)
        => Encoding.UTF8.GetString(responseBytes).Trim();

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

        await base.DisposeAsync();
        UnsubscribeProtocolEvents();
        await protocol.DisposeAsync();
        executionSemaphore.Dispose();
    }
}
