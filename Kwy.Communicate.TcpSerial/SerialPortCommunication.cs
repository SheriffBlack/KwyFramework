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

    public SerialPortCommunication(SerialPortConfig config) : base(config)
    {
        serialConfig = config ?? throw new ArgumentNullException(nameof(config));
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
            await Task.Run(() => port.Open(), cancellationToken);
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

        await serialPort.BaseStream.WriteAsync(data, cancellationToken);
    }

    protected override async Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (serialPort is not { IsOpen: true })
            throw new InvalidOperationException("Serial port is not open.");

        return await serialPort.BaseStream.ReadAsync(buffer, cancellationToken);
    }

    protected override bool ValidateConnection() => serialPort?.IsOpen == true;

    private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        => _ = HandleCommunicationFailureAsync(
            new IOException($"Serial port error: {e.EventType}"),
            $"Serial port error: {e.EventType}");
}
