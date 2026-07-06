using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.TcpSerial;
using Kwy.Device.Core;

namespace KwyTemplate.Device.Tcp;

public sealed class ExternalTcpDevice : DeviceBase, IExternalTcpDevice
{
    private readonly IByteTransport transport;

    public ExternalTcpDevice(
        string deviceId,
        string deviceName,
        ExternalTcpDeviceConnectionOptions config)
        : base(deviceId, deviceName, config)
    {
        ArgumentNullException.ThrowIfNull(config);
        transport = new TcpCommunication(config.ToTcpConfig());
        transport.ConnectionStateChanged += OnTransportStateChanged;
        transport.ErrorOccurred += OnTransportErrorOccurred;
    }

    public override string DeviceModel => "TCP/IP";

    public IByteTransport Transport => transport;

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        => transport.WriteAsync(data, cancellationToken);

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => transport.ReadAsync(buffer, cancellationToken);

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
        => transport.ConnectAsync(cancellationToken);

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
        => transport.DisconnectAsync(cancellationToken);

    protected override bool IsConnectionAlive()
        => transport.IsConnected;

    public override async Task ApplyConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (DeviceParameter is not ExternalTcpDeviceConnectionOptions config)
        {
            throw new InvalidOperationException("External TCP device configuration is invalid.");
        }

        if (!config.Validate())
        {
            throw new InvalidOperationException("External TCP device configuration validation failed.");
        }

        if (IsConnected)
        {
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        transport.ConnectionStateChanged -= OnTransportStateChanged;
        transport.ErrorOccurred -= OnTransportErrorOccurred;
        await base.DisposeAsync().ConfigureAwait(false);
        await transport.DisposeAsync().ConfigureAwait(false);
    }

    private void OnTransportStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        => RaiseStateChanged(e.CurrentState);

    private void OnTransportErrorOccurred(object? sender, ErrorOccurredEventArgs e)
        => RaiseErrorOccurred(e.Message, e.Exception);
}
