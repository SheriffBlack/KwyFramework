namespace Kwy.Device.IoCard.Abstractions;

/// <summary>
/// 定义模拟量原始信号与工程量之间的线性量程对应关系。
/// </summary>
/// <param name="RawMinimum">原始信号下限，例如 4 mA。</param>
/// <param name="RawMaximum">原始信号上限，例如 20 mA。</param>
/// <param name="EngineeringMinimum">工程量下限，例如 0 MPa。</param>
/// <param name="EngineeringMaximum">工程量上限，例如 10 MPa。</param>
public readonly record struct AnalogIoScale(
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
