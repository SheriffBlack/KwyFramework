using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;
using NationalInstruments.NI4882;
using System.Text;

namespace Kwy.Communicate.NI;

/// <summary>
/// Active-read NI4882 GPIB transport with command/query capability.
/// </summary>
public sealed class GpibCommunication : CommunicationBase, ICommandQueryClient
{
    private readonly GpibConfig gpibConfig;
    private readonly SemaphoreSlim ioSemaphore = new(1, 1);
    private Device? gpibDevice;
    private volatile bool disconnecting;

    public GpibCommunication(GpibConfig config) : base(config)
    {
        gpibConfig = config ?? throw new ArgumentNullException(nameof(config));
    }

    protected override Task ConnectInternalAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        disconnecting = false;
        gpibDevice = new Device(
            (byte)gpibConfig.BoardNumber,
            (byte)gpibConfig.PrimaryAddress,
            (byte)gpibConfig.SecondaryAddress);
        return Task.CompletedTask;
    }

    protected override async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        disconnecting = true;

        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, gpibConfig.Timeout));
        if (await ioSemaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                DisposeDeviceNoThrow();
            }
            finally
            {
                ioSemaphore.Release();
            }

            return;
        }

        OnErrorOccurred(
            new TimeoutException($"Timed out waiting for GPIB IO to finish within {timeout.TotalMilliseconds:0} ms."),
            "GPIB disconnect timed out while waiting for pending IO. The device handle will be disposed as a last resort.");
        DisposeDeviceNoThrow();
    }

    protected override Task SendInternalAsync(byte[] data, CancellationToken cancellationToken)
        => ExecuteIoAsync(device => device.Write(data), cancellationToken);

    protected override Task<int> ReceiveInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        => ExecuteIoAsync(device =>
        {
            var data = Encoding.ASCII.GetBytes(device.ReadString());
            var length = Math.Min(data.Length, buffer.Length);
            data.AsSpan(0, length).CopyTo(buffer.Span);
            return length;
        }, cancellationToken);

    protected override bool ValidateConnection() => gpibDevice != null;

    protected override async Task<bool> CheckConnectionAliveAsync(CancellationToken cancellationToken)
    {
        if (gpibDevice == null)
            return false;

        var keepAliveCommand = gpibConfig.KeepAliveCommand;
        if (string.IsNullOrWhiteSpace(keepAliveCommand))
            return true;

        try
        {
            await ExecuteIoAsync(device =>
            {
                device.Write(keepAliveCommand);
                if (keepAliveCommand.Contains('?'))
                {
                    _ = device.ReadString();
                }

                return true;
            }, cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnErrorOccurred(ex, $"GPIB KeepAlive failed: {ex.Message}");
            return false;
        }
    }

    public ValueTask WriteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return WriteAsync(Encoding.ASCII.GetBytes(command), cancellationToken);
    }

    public async ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return await ExecuteIoAsync(device =>
        {
            device.Write(command);
            return device.ReadString();
        }, cancellationToken);
    }

    private Device GetDevice()
    {
        if (disconnecting)
        {
            throw new InvalidOperationException("GPIB device is disconnecting.");
        }

        return gpibDevice ?? throw new InvalidOperationException("GPIB device is not open.");
    }

    private async Task ExecuteIoAsync(Action<Device> action, CancellationToken cancellationToken)
    {
        await ExecuteIoAsync(device =>
        {
            action(device);
            return true;
        }, cancellationToken);
    }

    private async Task<T> ExecuteIoAsync<T>(Func<Device, T> action, CancellationToken cancellationToken)
    {
        await ioSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (disconnecting)
            {
                throw new InvalidOperationException("GPIB device is disconnecting.");
            }

            return await Task.Run(() => action(GetDevice()), cancellationToken);
        }
        finally
        {
            ioSemaphore.Release();
        }
    }

    private void DisposeDeviceNoThrow()
    {
        var device = gpibDevice;
        gpibDevice = null;
        try
        {
            device?.Dispose();
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex, $"Dispose GPIB device failed: {ex.Message}");
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await base.DisposeAsync();
        ioSemaphore.Dispose();
    }
}
