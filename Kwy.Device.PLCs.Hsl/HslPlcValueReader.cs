namespace Kwy.Device.PLCs.Hsl;

internal static class HslPlcValueReader
{
    public static async Task<string> ReadAsStringAsync(
        HslPlcDevice device,
        HslPlcStatePoint point,
        CancellationToken cancellationToken)
    {
        return point.ValueType switch
        {
            HslPlcPointValueType.Bool => (await device.ReadBoolAsync(point.Address, cancellationToken)).ToString(),
            HslPlcPointValueType.Int16 => (await device.ReadInt16Async(point.Address, cancellationToken)).ToString(),
            HslPlcPointValueType.Int32 => (await device.ReadInt32ArrayAsync(point.Address, 1, cancellationToken))[0].ToString(),
            HslPlcPointValueType.Float => (await device.ReadFloatAsync(point.Address, cancellationToken)).ToString("G"),
            HslPlcPointValueType.Bytes => Convert.ToHexString(await device.ReadBytesAsync(point.Address, point.Length, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(point), point.ValueType, "Unsupported HSL PLC point value type.")
        };
    }
}
