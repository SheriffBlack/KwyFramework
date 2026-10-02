using Kwy.Device.Abstractions;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Core;
using TwinCAT.Ads;

namespace Kwy.Device.PLC.Beckhoff;

/// <summary>
/// TwinCAT ADS PLC 设备适配器。
/// <para>物理地址使用 TwinCAT PLC 符号名；业务流程应通过 <see cref="ILogicalPlcReader"/> 和 <see cref="ILogicalPlcWriter"/> 使用稳定点位 ID。</para>
/// </summary>
public sealed class BeckhoffAdsPlcDevice : PlcDeviceBase
{
    private readonly BeckhoffAdsPlcConfig config;
    private readonly AdsClient client = new();
    private readonly SemaphoreSlim ioSemaphore = new(1, 1);
    private volatile bool connected;

    public BeckhoffAdsPlcDevice(string deviceId, string deviceName, BeckhoffAdsPlcConfig config)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("TwinCAT ADS PLC 配置无效。", nameof(config));
        }
    }
    
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await client.ConnectAsync(AmsNetId.Parse(config.AmsNetId), config.AmsPort, cancellationToken).ConfigureAwait(false);
        connected = client.IsConnected;
        if (!connected)
        {
            throw new InvalidOperationException($"无法连接 TwinCAT ADS PLC：{config.AmsNetId}:{config.AmsPort}。");
        }
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connected = false;
        client.Disconnect();
        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive() => connected && client.IsConnected;

    public override Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        => ReadValueAsync<bool>(address, cancellationToken);

    public override Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        => ReadValueAsync<short>(address, cancellationToken);

    public override Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default)
        => ReadValueAsync<float>(address, cancellationToken);

    public override async Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default)
    {
        byte[] value = await ReadValueAsync<byte[]>(address, cancellationToken).ConfigureAwait(false);
        if (value.Length != length)
        {
            throw new InvalidOperationException($"ADS 符号“{address}”读取长度为 {value.Length}，与点位定义长度 {length} 不一致。");
        }

        return value;
    }

    public override Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadArrayAsync<short>(address, count, cancellationToken);

    public override Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadArrayAsync<int>(address, count, cancellationToken);

    public override Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadArrayAsync<float>(address, count, cancellationToken);

    public override Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        => WriteValueAsync(address, value, cancellationToken);

    public override Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        => WriteValueAsync(address, value, cancellationToken);

    public override Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        => WriteValueAsync(address, value, cancellationToken);

    public override Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default)
        => WriteValueAsync(address, value, cancellationToken);

    public override Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return WriteValueAsync(address, data, cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await base.DisposeAsync().ConfigureAwait(false);
        client.Dispose();
        ioSemaphore.Dispose();
    }

    private async Task<T> ReadValueAsync<T>(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        EnsureConnected();

        await ioSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = CreateOperationTimeout(cancellationToken);
            ResultValue<T> result = await client.ReadValueAsync<T>(address, timeout.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"读取 ADS 符号“{address}”失败：{result.ErrorCode}。");
            }

            if (result.Value is null)
            {
                throw new InvalidOperationException($"读取 ADS 符号“{address}”返回空值。");
            }

            return result.Value;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RaiseOperationOccurred(DeviceOperationKind.Read, address, false, $"读取 ADS 符号失败：{exception.Message}", exception, CreateDiagnostics(address));
            throw;
        }
        finally
        {
            ioSemaphore.Release();
        }
    }

    private async Task<T[]> ReadArrayAsync<T>(string address, ushort count, CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        T[] value = await ReadValueAsync<T[]>(address, cancellationToken).ConfigureAwait(false);
        if (value.Length != count)
        {
            throw new InvalidOperationException($"ADS 符号“{address}”读取数组长度为 {value.Length}，与请求长度 {count} 不一致。");
        }

        return value;
    }

    private async Task WriteValueAsync<T>(string address, T value, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        EnsureConnected();

        await ioSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = CreateOperationTimeout(cancellationToken);
            ResultWrite result = await client.WriteValueAsync(address, value, timeout.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"写入 ADS 符号“{address}”失败：{result.ErrorCode}。");
            }

            RaiseOperationOccurred(DeviceOperationKind.Write, address, true, "ADS 符号写入成功。", properties: CreateDiagnostics(address));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RaiseOperationOccurred(DeviceOperationKind.Write, address, false, $"写入 ADS 符号失败：{exception.Message}", exception, CreateDiagnostics(address));
            throw;
        }
        finally
        {
            ioSemaphore.Release();
        }
    }

    private CancellationTokenSource CreateOperationTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(config.OperationTimeoutMilliseconds);
        return timeout;
    }

    private IReadOnlyDictionary<string, string> CreateDiagnostics(string address)
        => new Dictionary<string, string>
        {
            ["Protocol"] = "TwinCAT ADS",
            ["AmsNetId"] = config.AmsNetId,
            ["AmsPort"] = config.AmsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Address"] = address
        };

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new InvalidOperationException("TwinCAT ADS PLC 未连接。");
        }
    }
}
