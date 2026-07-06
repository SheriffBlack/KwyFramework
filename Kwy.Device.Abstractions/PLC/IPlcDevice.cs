namespace Kwy.Device.Abstractions.PLC;

public interface IPlcReader
{
    Task<bool> ReadBoolAsync(string address, CancellationToken cancellationToken = default);
    Task<short> ReadInt16Async(string address, CancellationToken cancellationToken = default);
    Task<float> ReadFloatAsync(string address, CancellationToken cancellationToken = default);
    Task<byte[]> ReadBytesAsync(string address, ushort length, CancellationToken cancellationToken = default);
    Task<short[]> ReadInt16ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    Task<int[]> ReadInt32ArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
    Task<float[]> ReadFloatArrayAsync(string address, ushort count, CancellationToken cancellationToken = default);
}

public interface IPlcWriter
{
    Task WriteBoolAsync(string address, bool value, CancellationToken cancellationToken = default);
    Task WriteInt16Async(string address, short value, CancellationToken cancellationToken = default);
    Task WriteInt32Async(string address, int value, CancellationToken cancellationToken = default);
    Task WriteFloatAsync(string address, float value, CancellationToken cancellationToken = default);
    Task WriteBytesAsync(string address, byte[] data, CancellationToken cancellationToken = default);
}

public interface IPlcPointRegistry
{
    void RegisterPoint(string address, string name, Type dataType, bool isReadOnly = false);
    IEnumerable<PlcPointInfoModel> GetAllRegisteredPoints();
}

public interface IPlcDevice :
    IDevice,
    IConfigurableDevice,
    IPlcReader,
    IPlcWriter,
    IPlcPointRegistry
{
}
