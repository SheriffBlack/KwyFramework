using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.PLC;

namespace Kwy.Device.Core.PLC;

/// <summary>
/// PLC 业务点位读写服务。负责把稳定点位 ID 解析为设备与物理地址，并执行权限和类型校验。
/// </summary>
public sealed class LogicalPlcService : ILogicalPlcReader, ILogicalPlcWriter
{
    private readonly IPlcPointDefinitionProvider definitions;
    private readonly IDeviceRegistry devices;

    /// <summary>创建 PLC 业务点位读写服务。</summary>
    public LogicalPlcService(IPlcPointDefinitionProvider definitions, IDeviceRegistry devices)
    {
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
    }

    /// <inheritdoc/>
    public async Task<T> ReadAsync<T>(string pointId, CancellationToken cancellationToken = default)
    {
        PlcPointDefinition point = definitions.GetRequired(pointId);
        if (point.Access == PlcPointAccess.WriteOnly)
            throw new InvalidOperationException($"PLC 点位“{point.Id}”为只写点位。");

        IPlcDevice plc = devices.GetRequiredDevice<IPlcDevice>(point.DeviceId);
        object value = point.DataType switch
        {
            PlcDataType.Boolean => await plc.ReadBoolAsync(point.Address, cancellationToken).ConfigureAwait(false),
            PlcDataType.Int16 => await plc.ReadInt16Async(point.Address, cancellationToken).ConfigureAwait(false),
            PlcDataType.UInt16 => unchecked((ushort)await plc.ReadInt16Async(point.Address, cancellationToken).ConfigureAwait(false)),
            PlcDataType.Int32 => (await plc.ReadInt32ArrayAsync(point.Address, 1, cancellationToken).ConfigureAwait(false))[0],
            PlcDataType.UInt32 => unchecked((uint)(await plc.ReadInt32ArrayAsync(point.Address, 1, cancellationToken).ConfigureAwait(false))[0]),
            PlcDataType.Float => await plc.ReadFloatAsync(point.Address, cancellationToken).ConfigureAwait(false),
            PlcDataType.Bytes => await plc.ReadBytesAsync(point.Address, point.Length, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(point), point.DataType, "不支持的 PLC 数据类型。")
        };

        if (value is T typed) return typed;
        throw new InvalidOperationException($"PLC 点位“{point.Id}”的数据类型为 {point.DataType}，与请求类型 {typeof(T).Name} 不一致。");
    }

    /// <inheritdoc/>
    public async Task WriteAsync<T>(string pointId, T value, CancellationToken cancellationToken = default)
    {
        PlcPointDefinition point = definitions.GetRequired(pointId);
        if (point.Access == PlcPointAccess.ReadOnly)
            throw new InvalidOperationException($"PLC 点位“{point.Id}”为只读点位。");

        IPlcDevice plc = devices.GetRequiredDevice<IPlcDevice>(point.DeviceId);
        switch (point.DataType, value)
        {
            case (PlcDataType.Boolean, bool actual): await plc.WriteBoolAsync(point.Address, actual, cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.Int16, short actual): await plc.WriteInt16Async(point.Address, actual, cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.UInt16, ushort actual): await plc.WriteInt16Async(point.Address, unchecked((short)actual), cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.Int32, int actual): await plc.WriteInt32Async(point.Address, actual, cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.UInt32, uint actual): await plc.WriteInt32Async(point.Address, unchecked((int)actual), cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.Float, float actual): await plc.WriteFloatAsync(point.Address, actual, cancellationToken).ConfigureAwait(false); break;
            case (PlcDataType.Bytes, byte[] actual): await plc.WriteBytesAsync(point.Address, actual, cancellationToken).ConfigureAwait(false); break;
            default: throw new InvalidOperationException($"值类型 {typeof(T).Name} 与 PLC 点位“{point.Id}”声明的数据类型 {point.DataType} 不一致。");
        }
    }
}
