using Kwy.Device.PLC.Abstractions;

namespace Kwy.Device.PLC.Core;

/// <summary>在 PLC 寄存器原始数值与工程量之间执行线性换算。</summary>
public static class PlcValueConverter
{
    /// <summary>将寄存器原始值换算为工程量。</summary>
    public static double ToEngineeringValue(double rawValue, PlcValueScale scale)
    {
        if (!double.IsFinite(rawValue)) throw new ArgumentOutOfRangeException(nameof(rawValue));
        scale.Validate();
        return (rawValue - scale.RawMinimum) / (scale.RawMaximum - scale.RawMinimum)
            * (scale.EngineeringMaximum - scale.EngineeringMinimum) + scale.EngineeringMinimum;
    }

    /// <summary>将工程量换算为寄存器原始数值。</summary>
    public static double ToRawValue(double engineeringValue, PlcValueScale scale)
    {
        if (!double.IsFinite(engineeringValue)) throw new ArgumentOutOfRangeException(nameof(engineeringValue));
        scale.Validate();
        return (engineeringValue - scale.EngineeringMinimum) / (scale.EngineeringMaximum - scale.EngineeringMinimum)
            * (scale.RawMaximum - scale.RawMinimum) + scale.RawMinimum;
    }
}
