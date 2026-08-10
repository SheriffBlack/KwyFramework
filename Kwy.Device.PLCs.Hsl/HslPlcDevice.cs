using HslCommunication;
using HslCommunication.Core;
using HslCommunication.ModBus;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Core.PLC;

namespace Kwy.Device.PLCs.Hsl;

public class HslPlcDevice : PlcDeviceBase, IModbusPlcReader
{
    private readonly HslPlcClientSession session;
    private readonly HslPlcConfig config;
    private readonly object lifecycleSync = new();
    private SemaphoreSlim ioSemaphore = new(1, 1);
    private bool connected;

    public override string DeviceModel => $"{config.Brand}/{config.Transport}";

    public HslPlcDevice(string deviceId, string deviceName, HslPlcConfig config)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        session = HslPlcClientFactory.Create(config);
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        RotateIoSemaphore();
        var result = await ExecuteIoAsync(session.Connect, cancellationToken);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"[HslPlc] Connection failed ({session.Description}, {config.IpAddress}:{config.Port}, Timeout={config.ConnectTimeoutMilliseconds}/{config.ReceiveTimeoutMilliseconds}ms): {result.Message}");
        }

        connected = true;
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (!connected)
        {
            return;
        }

        connected = false;
        RotateIoSemaphore();
        try
        {
            await Task.Run(() => session.Disconnect(), CancellationToken.None)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions during disconnect to ensure resources are cleaned up.
        }
    }

    protected override bool IsConnectionAlive() => connected;

    public override Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadBool(address), $"Bool address {address}", cancellationToken);

    public Task<bool> ReadCoilAsync(string address, CancellationToken cancellationToken = default)
        => ReadAsync(() => ReadModbusCoil(address), $"Modbus coil address {address}", cancellationToken);

    public Task<bool> ReadDiscreteAsync(string address, CancellationToken cancellationToken = default)
        => ReadAsync(() => ReadModbusDiscrete(address), $"Modbus discrete input address {address}", cancellationToken);

    public override Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default)
        => WriteAsync(() => session.Client.Write(address, value), $"Bool address {address}", cancellationToken);

    public override Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadInt16(address), $"Int16 address {address}", cancellationToken);

    public override Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
        => WriteAsync(() => session.Client.Write(address, value), $"Int16 address {address}", cancellationToken);

    public override Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
        => WriteAsync(() => session.Client.Write(address, value), $"Int32 address {address}", cancellationToken);

    public override Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadFloat(address), $"Float address {address}", cancellationToken);

    public override Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default)
        => WriteAsync(() => session.Client.Write(address, value), $"Float address {address}", cancellationToken);

    public override Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.Read(address, length), $"byte address {address}", cancellationToken);

    public override Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default)
        => WriteAsync(() => session.Client.Write(address, data), $"byte address {address}", cancellationToken);

    public override Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadInt16(address, count), $"Int16[] address {address}", cancellationToken);

    public override Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadInt32(address, count), $"Int32[] address {address}", cancellationToken);

    public override Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default)
        => ReadAsync(() => session.Client.ReadFloat(address, count), $"Float[] address {address}", cancellationToken);

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await base.DisposeAsync();
        if (session.Client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        ioSemaphore.Dispose();
    }

    private OperateResult<bool> ReadModbusCoil(string address)
    {
        return session.Client switch
        {
            ModbusRtu rtu => rtu.ReadCoil(address),
            ModbusTcpNet tcp => tcp.ReadCoil(address),
            _ => new OperateResult<bool>($"Current PLC client does not support Modbus coil read: {session.Client.GetType().Name}.")
        };
    }

    private OperateResult<bool> ReadModbusDiscrete(string address)
    {
        return session.Client switch
        {
            ModbusRtu rtu => rtu.ReadDiscrete(address),
            ModbusTcpNet tcp => tcp.ReadDiscrete(address),
            _ => new OperateResult<bool>($"Current PLC client does not support Modbus discrete input read: {session.Client.GetType().Name}.")
        };
    }
    private async Task<T> ReadAsync<T>(Func<OperateResult<T>> operation, string description, CancellationToken cancellationToken)
    {
        bool reported = false;
        try
        {
            EnsureConnected();
            var result = await ExecuteIoAsync(operation, cancellationToken);
            if (!result.IsSuccess)
            {
                string message = $"[HslPlc] Read {description} failed: {result.Message}";
                var exception = new InvalidOperationException(message);
                RaisePlcOperationFailed(DeviceOperationKind.Read, description, message, exception);
                reported = true;
                throw exception;
            }

            return result.Content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!reported)
            {
                RaisePlcOperationFailed(DeviceOperationKind.Read, description, $"[HslPlc] Read {description} failed: {ex.Message}", ex);
            }

            throw;
        }
    }

    private async Task WriteAsync(Func<OperateResult> operation, string description, CancellationToken cancellationToken)
    {
        bool reported = false;
        try
        {
            EnsureConnected();
            var result = await ExecuteIoAsync(operation, cancellationToken);
            if (!result.IsSuccess)
            {
                string message = $"[HslPlc] Write {description} failed: {result.Message}";
                var exception = new InvalidOperationException(message);
                RaisePlcOperationFailed(DeviceOperationKind.Write, description, message, exception);
                reported = true;
                throw exception;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!reported)
            {
                RaisePlcOperationFailed(DeviceOperationKind.Write, description, $"[HslPlc] Write {description} failed: {ex.Message}", ex);
            }

            throw;
        }
    }

    private void RaisePlcOperationFailed(DeviceOperationKind kind, string description, string message, Exception exception)
        => RaiseOperationOccurred(
            kind,
            description,
            false,
            message,
            exception,
            new Dictionary<string, string>
            {
                ["Protocol"] = session.Description,
                ["Endpoint"] = $"{config.IpAddress}:{config.Port}"
            });
    private async Task<T> ExecuteIoAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        SemaphoreSlim semaphore = ioSemaphore;
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(operation, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void RotateIoSemaphore()
    {
        lock (lifecycleSync)
        {
            ioSemaphore = new SemaphoreSlim(1, 1);
        }
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            throw new InvalidOperationException("PLC is not connected.");
        }
    }

}




