namespace Kwy.Device.Abstractions.PLC;

/// <summary>PLC 物理地址读取能力；业务流程应优先使用 <see cref="ILogicalPlcReader"/> 按点位 ID 读取。</summary>
public interface IPlcReader
{
    /// <summary>读取布尔值。</summary>
    Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default);
    /// <summary>读取 16 位有符号整数。</summary>
    Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default);
    /// <summary>读取单精度浮点数。</summary>
    Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default);
    /// <summary>读取指定长度的原始字节数据。</summary>
    Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default);
    /// <summary>连续读取一组 16 位有符号整数。</summary>
    Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    /// <summary>连续读取一组 32 位有符号整数。</summary>
    Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    /// <summary>连续读取一组单精度浮点数。</summary>
    Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
}

/// <summary>Modbus PLC 特有的线圈与离散输入读取能力。</summary>
public interface IModbusPlcReader
{
    /// <summary>读取线圈状态。</summary>
    Task<bool> ReadCoilAsync(string address, CancellationToken cancellationToken = default);
    /// <summary>读取离散输入状态。</summary>
    Task<bool> ReadDiscreteAsync(string address, CancellationToken cancellationToken = default);
}

/// <summary>PLC 物理地址写入能力；业务流程应优先使用 <see cref="ILogicalPlcWriter"/> 按点位 ID 写入。</summary>
public interface IPlcWriter
{
    /// <summary>写入布尔值。</summary>
    Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default);
    /// <summary>写入 16 位有符号整数。</summary>
    Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default);
    /// <summary>写入 32 位有符号整数。</summary>
    Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default);
    /// <summary>写入单精度浮点数。</summary>
    Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default);
    /// <summary>写入原始字节数据。</summary>
    Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default);
}

/// <summary>PLC 设备的厂商无关契约，组合设备生命周期、配置和物理地址读写能力。</summary>
public interface IPlcDevice :
    IDevice,
    IConfigurableDevice,
    IPlcReader,
    IPlcWriter
{
}
