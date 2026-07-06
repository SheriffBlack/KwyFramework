using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using Kwy.Communicate.Abstractions.Enums;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.PLC;

/// <summary>
/// Base class for asynchronous PLC devices and point metadata management.
/// </summary>
public abstract class PlcDeviceBase : DeviceBase, IPlcDevice
{
    private readonly ConcurrentDictionary<string, PlcPointInfoModel> registeredPoints = new();
    private readonly object keepAliveSync = new();
    private CancellationTokenSource? keepAliveCancellation;

    protected PlcDeviceBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
    }

    public abstract Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default);
    public abstract Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default);
    public abstract Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default);
    public abstract Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default);
    public abstract Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default);
    public abstract Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default);
    public abstract Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default);
    public abstract Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default);
    public abstract Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default);
    public abstract Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    public abstract Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    public abstract Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);

    protected virtual async Task ExecuteKeepAliveAsync(PlcConfig config, CancellationToken cancellationToken)
    {
        switch (config.KeepAliveMode)
        {
            case PlcKeepAliveMode.ReadBool:
                _ = await ReadBoolAsync(config.KeepAliveAddress!, cancellationToken);
                break;
            case PlcKeepAliveMode.ReadInt16:
                _ = await ReadInt16Async(config.KeepAliveAddress!, cancellationToken);
                break;
            case PlcKeepAliveMode.ReadInt32:
                _ = await ReadInt32ArrayAsync(config.KeepAliveAddress!, 1, cancellationToken);
                break;
            case PlcKeepAliveMode.ReadFloat:
                _ = await ReadFloatAsync(config.KeepAliveAddress!, cancellationToken);
                break;
            case PlcKeepAliveMode.ReadBytes:
                _ = await ReadBytesAsync(config.KeepAliveAddress!, 1, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(config), config.KeepAliveMode, "Unsupported PLC KeepAlive mode.");
        }
    }

    protected override Task OnConnectedAsync(CancellationToken cancellationToken)
    {
        StartKeepAlive();
        return Task.CompletedTask;
    }

    protected override Task OnDisconnectingAsync(CancellationToken cancellationToken)
    {
        StopKeepAlive();
        return Task.CompletedTask;
    }

    public void RegisterPoint(string address, string name, Type dataType, bool isReadOnly = false)
    {
        registeredPoints[address] = new PlcPointInfoModel
        {
            Address = address,
            Name = name,
            DataType = dataType,
            IsReadOnly = isReadOnly
        };
    }

    public IEnumerable<PlcPointInfoModel> GetAllRegisteredPoints() => registeredPoints.Values;

    private void StartKeepAlive()
    {
        if (DeviceParameter is not PlcConfig { KeepAlive: true } plcConfig ||
            string.IsNullOrWhiteSpace(plcConfig.KeepAliveAddress))
        {
            return;
        }

        lock (keepAliveSync)
        {
            keepAliveCancellation?.Cancel();
            keepAliveCancellation?.Dispose();
            keepAliveCancellation = new CancellationTokenSource();
            _ = KeepAliveLoopAsync(plcConfig, keepAliveCancellation.Token);
        }
    }

    private async Task KeepAliveLoopAsync(PlcConfig plcConfig, CancellationToken cancellationToken)
    {
        var interval = Math.Max(plcConfig.KeepAliveInterval, 1000);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);

                if (!IsConnected)
                {
                    continue;
                }

                await ExecuteKeepAliveAsync(plcConfig, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (State is ConnectionState.Error or ConnectionState.Disconnected)
            {
                return;
            }

            await HandleKeepAliveFailureAsync(ex);
        }
    }

    private async Task HandleKeepAliveFailureAsync(Exception exception)
    {
        StopKeepAlive();
        await HandleDeviceFailureAsync($"PLC KeepAlive failed: {exception.Message}", exception);
    }

    private void StopKeepAlive()
    {
        lock (keepAliveSync)
        {
            keepAliveCancellation?.Cancel();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        StopKeepAlive();
        await base.DisposeAsync();
        keepAliveCancellation?.Dispose();
    }
}
