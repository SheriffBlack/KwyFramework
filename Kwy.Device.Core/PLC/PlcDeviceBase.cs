using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using Kwy.Communicate.Abstractions.Enums;

namespace Kwy.Device.Core.PLC;

/// <summary>
/// 异步 PLC 设备基类，提供设备生命周期与协议层心跳管理。
/// </summary>
public abstract class PlcDeviceBase : DeviceBase, IPlcDevice
{
    private readonly object keepAliveSync = new();
    private CancellationTokenSource? keepAliveCancellation;

    /// <summary>使用设备身份和配置创建 PLC 设备。</summary>
    protected PlcDeviceBase(string deviceId, string deviceName, IDeviceConfig config)
        : base(deviceId, deviceName, config)
    {
    }

    /// <inheritdoc/>
    public abstract Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    /// <inheritdoc/>
    public abstract Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);

    /// <summary>按照心跳配置执行一次读取；厂商适配器可重写此方法实现协议专属心跳。</summary>
    protected virtual async Task ExecuteKeepAliveAsync(IPlcKeepAliveConfig config, CancellationToken cancellationToken)
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
                throw new ArgumentOutOfRangeException(nameof(config), config.KeepAliveMode, "不支持的 PLC 心跳读取方式。");
        }
    }

    /// <inheritdoc/>
    protected override Task OnConnectedAsync(CancellationToken cancellationToken)
    {
        StartKeepAlive();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task OnDisconnectingAsync(CancellationToken cancellationToken)
    {
        StopKeepAlive();
        return Task.CompletedTask;
    }

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
        await HandleDeviceFailureAsync($"PLC 心跳失败：{exception.Message}", exception);
    }

    private void StopKeepAlive()
    {
        lock (keepAliveSync)
        {
            keepAliveCancellation?.Cancel();
        }
    }

    /// <inheritdoc/>
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
