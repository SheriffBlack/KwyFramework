using Kwy.Communicate.Core;
using Kwy.Communicate.TcpSerial.Configs;
using System.IO.Ports;

namespace Kwy.Communicate.TcpSerial;

/// <summary>
/// Active-read serial port byte transport.
/// </summary>
public sealed class SerialPortCommunication : CommunicationBase
{
    private readonly SerialPortConfig serialConfig;
    private SerialPort? serialPort;

    public SerialPortCommunication(SerialPortConfig config) : base(CloneConfig(config))
    {
        serialConfig = (SerialPortConfig)this.config;
    }

    protected override async Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        var availablePorts = SerialPort.GetPortNames();
        if (!Array.Exists(availablePorts, port => string.Equals(port, serialConfig.Port, StringComparison.OrdinalIgnoreCase)))
        {
            throw new System.IO.IOException($"Serial port '{serialConfig.Port}' does not exist on this machine. Available ports: {string.Join(", ", availablePorts)}");
        }

        var port = new SerialPort
        {
            PortName = serialConfig.Port,
            BaudRate = serialConfig.BaudRate,
            Parity = (Parity)serialConfig.Parity,
            DataBits = serialConfig.DataBits,
            StopBits = (StopBits)serialConfig.StopBits,
            Handshake = (Handshake)serialConfig.Handshake,
            ReadTimeout = serialConfig.ReadTimeout > 0 ? serialConfig.ReadTimeout : 100,
            WriteTimeout = serialConfig.WriteTimeout > 0 ? serialConfig.WriteTimeout : 100
        };

        port.ErrorReceived += SerialPort_ErrorReceived;

        try
        {
            Task openTask = Task.Run(port.Open, CancellationToken.None);
            await openTask.WaitAsync(TimeSpan.FromMilliseconds(serialConfig.Timeout), cancellationToken);
            serialPort = port;
        }
        catch
        {
            port.ErrorReceived -= SerialPort_ErrorReceived;
            port.Dispose();
            throw;
        }
    }

    protected override Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        var port = serialPort;
        serialPort = null;

        if (port != null)
        {
            port.ErrorReceived -= SerialPort_ErrorReceived;
            try
            {
                // Directly disposing the SerialPort closes the port and releases resources.
                // This is safer than calling Close() which can hang on faulty or unplugged USB-serial drivers.
                port.Dispose();
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    protected override async Task SendInternalAsync(byte[] data, CancellationToken cancellationToken)
    {
        if (serialPort is not { IsOpen: true })
            throw new InvalidOperationException("Serial port is not open.");

        using var timeout = CreateOperationTimeout(cancellationToken, serialConfig.WriteTimeout);
        try
        {
            await serialPort.BaseStream.WriteAsync(data, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Serial-port write timed out after {serialConfig.WriteTimeout} ms.");
        }
    }

    protected override async Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (serialPort is not { IsOpen: true })
            throw new InvalidOperationException("Serial port is not open.");

        using var timeout = CreateOperationTimeout(cancellationToken, serialConfig.ReadTimeout);
        try
        {
            return await serialPort.BaseStream.ReadAsync(buffer, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Serial-port read timed out after {serialConfig.ReadTimeout} ms.");
        }
    }

    protected override bool ValidateConnection() => serialPort?.IsOpen == true;

    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        => _ = HandleCommunicationFailureAsync(
            new IOException($"Serial port error: {e.EventType}"),
            $"Serial port error: {e.EventType}");

    private static CancellationTokenSource CreateOperationTimeout(CancellationToken cancellationToken, int timeoutMs)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs > 0)
            timeout.CancelAfter(timeoutMs);
        return timeout;
    }

    private static SerialPortConfig CloneConfig(SerialPortConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SerialPortConfig
        {
            Port = config.Port,
            BaudRate = config.BaudRate,
            Parity = config.Parity,
            DataBits = config.DataBits,
            StopBits = config.StopBits,
            Handshake = config.Handshake,
            ReadTimeout = config.ReadTimeout,
            WriteTimeout = config.WriteTimeout,
            KeepAlive = config.KeepAlive,
            KeepAliveInterval = config.KeepAliveInterval,
            Timeout = config.Timeout,
            AutoReconnect = config.AutoReconnect,
            MaxReconnectAttempts = config.MaxReconnectAttempts,
            ReconnectInterval = config.ReconnectInterval
        };
    }
}
