using Kwy.Device.IoCard.Abstractions;

namespace Kwy.Device.IoCard.Core;

/// <summary>在模拟量原始信号与工程量之间执行线性换算。</summary>
public static class AnalogIoValueConverter
{
    /// <summary>将原始信号换算为工程量。</summary>
    public static double ToEngineeringValue(double rawValue, AnalogIoScale scale)
    {
        if (!double.IsFinite(rawValue)) throw new ArgumentOutOfRangeException(nameof(rawValue));
        scale.Validate();
        return (rawValue - scale.RawMinimum) / (scale.RawMaximum - scale.RawMinimum)
            * (scale.EngineeringMaximum - scale.EngineeringMinimum) + scale.EngineeringMinimum;
    }

    /// <summary>将工程量换算为设备原始信号。</summary>
    public static double ToRawValue(double engineeringValue, AnalogIoScale scale)
    {
        if (!double.IsFinite(engineeringValue)) throw new ArgumentOutOfRangeException(nameof(engineeringValue));
        scale.Validate();
        return (engineeringValue - scale.EngineeringMinimum) / (scale.EngineeringMaximum - scale.EngineeringMinimum)
            * (scale.RawMaximum - scale.RawMinimum) + scale.RawMinimum;
    }
}
