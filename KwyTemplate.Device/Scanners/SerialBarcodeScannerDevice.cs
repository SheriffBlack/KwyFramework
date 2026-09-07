using System.Text;
using System.IO.Ports;
using System.Diagnostics;
using Kwy.Device.Abstractions;
using Kwy.Device.Core;

namespace KwyTemplate.Device.Scanners;

public sealed class SerialBarcodeScannerDevice : DeviceBase, IBarcodeScannerDevice
{
    private static readonly byte[] TriggerCommand = [0x02, 0xF4, 0x03];
    private const int ReadBufferSize = 1024;
    private static readonly TimeSpan ReceiveIdleWindow = TimeSpan.FromMilliseconds(30);

    private SerialPort? serialPort;

    public SerialBarcodeScannerDevice(
        string deviceId,
        string deviceName,
        BarcodeScannerConfig config)
        : base(deviceId, deviceName, config)
    {
    }

    public override string DeviceModel => "Serial Barcode Scanner";

    public string? LastCode { get; private set; }

    private BarcodeScannerConfig Config => (BarcodeScannerConfig)DeviceParameter;

    public event EventHandler<BarcodeScannedEventArgs>? CodeReceived;

    public async Task TriggerScanAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();
            if (serialPort is not { IsOpen: true } port)
            {
                throw new InvalidOperationException("Barcode scanner is not connected.");
            }

            await Task.Run(() =>
            {
                port.DiscardOutBuffer();
                port.Write(TriggerCommand, 0, TriggerCommand.Length);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationOccurred(DeviceOperationKind.Trigger, "TriggerScan", false, $"Barcode scanner trigger failed. Device={DeviceName}, Error={ex.Message}", ex);
            throw;
        }
    }

    public async Task<string> WaitForCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        try
        {
            if (serialPort is not { IsOpen: true } port)
            {
                throw new InvalidOperationException("Barcode scanner is not connected.");
            }

            // Same behavior as the legacy ScannerGun: one direct SerialPort.Read
            // after the trigger, then return all bytes received by that read.
            port.ReadTimeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalMilliseconds));
            byte[] buffer = new byte[ReadBufferSize];
            int length = await Task.Run(() => ReadFrame(port, buffer), cancellationToken).ConfigureAwait(false);
            port.DiscardInBuffer();
            string code = Encoding.ASCII.GetString(buffer, 0, Math.Max(length, 0)).Trim();
            string rawHex = Convert.ToHexString(buffer, 0, Math.Max(length, 0));
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new TimeoutException("Barcode scanner returned no data.");
            }

            RaiseOperationOccurred(
                DeviceOperationKind.Read,
                "WaitForCode",
                true,
                $"Barcode scanner data received. Device={DeviceName}, Port={port.PortName}, BaudRate={port.BaudRate}, Parity={port.Parity}, DataBits={port.DataBits}, StopBits={port.StopBits}, Handshake={port.Handshake}, Hex={rawHex}, Text={code}");
            PublishCode(code, code);
            return code;
        }
        catch (TimeoutException)
        {
            var exception = new TimeoutException("Timed out waiting for barcode scanner data.");
            RaiseOperationOccurred(DeviceOperationKind.Read, "WaitForCode", false, $"Barcode scanner read failed. Device={DeviceName}, Error={exception.Message}", exception);
            throw exception;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RaiseOperationOccurred(DeviceOperationKind.Read, "WaitForCode", false, $"Barcode scanner read failed. Device={DeviceName}, Error={ex.Message}", ex);
            throw;
        }
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        if (!Config.Validate())
        {
            throw new InvalidOperationException("Barcode scanner configuration is invalid.");
        }

        // Keep the same wire settings as the proven legacy ScannerGun.
        // The machine setting only supplies the physical COM port.
        var port = new SerialPort(
            Config.Serial.Port,
            9600,
            Parity.None,
            8,
            StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        try
        {
            await Task.Run(port.Open, cancellationToken).ConfigureAwait(false);
            port.DiscardOutBuffer();
            port.DiscardInBuffer();
            serialPort = port;
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        SerialPort? port = serialPort;
        serialPort = null;
        if (port == null)
        {
            return Task.CompletedTask;
        }

        port.Dispose();
        return Task.CompletedTask;
    }
    protected override bool IsConnectionAlive()
        => serialPort?.IsOpen == true;

    public override Task ApplyConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!Config.Validate())
        {
            throw new InvalidOperationException("Barcode scanner configuration is invalid.");
        }

        return Task.CompletedTask;
    }

    private void PublishCode(string code, string rawText)
    {
        string normalized = code.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        LastCode = normalized;
        var args = new BarcodeScannedEventArgs(normalized, rawText, DateTimeOffset.Now);
        CodeReceived?.Invoke(this, args);

    }

    private static int ReadFrame(SerialPort port, byte[] buffer)
    {
        int totalLength = port.Read(buffer, 0, buffer.Length);
        var idleTimer = Stopwatch.StartNew();

        while (totalLength < buffer.Length && idleTimer.Elapsed < ReceiveIdleWindow)
        {
            int available = port.BytesToRead;
            if (available > 0)
            {
                int readLength = port.Read(buffer, totalLength, Math.Min(available, buffer.Length - totalLength));
                totalLength += readLength;
                idleTimer.Restart();
                continue;
            }

            Thread.Sleep(2);
        }

        return totalLength;
    }
}

