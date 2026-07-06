namespace Kwy.Device.PLCs.Hsl;

public sealed class HslPlcRuntimeOptions
{
    public IList<HslPlcStatePoint> StatePoints { get; } = new List<HslPlcStatePoint>();

    public IList<HslPlcSafetyPoint> SafetyPoints { get; } = new List<HslPlcSafetyPoint>();
}

public sealed record HslPlcStatePoint(
    string Name,
    string Address,
    HslPlcPointValueType ValueType = HslPlcPointValueType.Bool,
    ushort Length = 1);

public sealed record HslPlcSafetyPoint(
    string Name,
    string Address,
    bool ExpectedValue = true,
    string? Code = null,
    string? Message = null);
