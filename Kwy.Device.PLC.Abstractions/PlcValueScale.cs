namespace Kwy.Device.PLC.Abstractions;

/// <summary>
/// 定义 PLC 寄存器原始数值与工程量之间的线性量程对应关系。
/// </summary>
/// <param name="RawMinimum">寄存器原始值下限。</param>
/// <param name="RawMaximum">寄存器原始值上限。</param>
/// <param name="EngineeringMinimum">工程量下限。</param>
/// <param name="EngineeringMaximum">工程量上限。</param>
public readonly record struct PlcValueScale(
    double RawMinimum,
    double RawMaximum,
    double EngineeringMinimum,
    double EngineeringMaximum)
{
    /// <summary>校验原始量程和工程量程均为有限的递增区间。</summary>
    public void Validate()
    {
        ValidateRange(RawMinimum, RawMaximum, nameof(RawMinimum), nameof(RawMaximum));
        ValidateRange(EngineeringMinimum, EngineeringMaximum, nameof(EngineeringMinimum), nameof(EngineeringMaximum));
    }

    private static void ValidateRange(double minimum, double maximum, string minimumName, string maximumName)
    {
        if (!double.IsFinite(minimum)) throw new ArgumentOutOfRangeException(minimumName);
        if (!double.IsFinite(maximum) || maximum <= minimum) throw new ArgumentOutOfRangeException(maximumName);
    }
}
