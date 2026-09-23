using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 负责工程单位与控制器原生脉冲单位之间的唯一换算入口。
/// 工艺层只使用毫米、角度或明确的工程单位，不应自行计算脉冲或方向反转。
/// </summary>
public static class AxisEngineeringConverter
{
    public static double ToNativePosition(double value, AxisEngineeringConfig config)
        => config.Unit == MotionUnit.Pulse
            ? ApplyDirection(value, config)
            : ApplyDirection(value * config.PulsesPerUnit, config);

    public static double FromNativePosition(double value, AxisEngineeringConfig config)
        => config.Unit == MotionUnit.Pulse
            ? ApplyDirection(value, config)
            : ApplyDirection(value, config) / config.PulsesPerUnit;

    public static double ToNativeVelocity(double value, AxisEngineeringConfig config)
        => config.Unit == MotionUnit.Pulse
            ? ApplyDirection(value, config)
            : ApplyDirection(value * config.PulsesPerUnit / 1000d, config);

    public static double FromNativeVelocity(double value, AxisEngineeringConfig config)
        => config.Unit == MotionUnit.Pulse
            ? ApplyDirection(value, config)
            : ApplyDirection(value, config) * 1000d / config.PulsesPerUnit;

    public static double ToNativeAcceleration(double value, AxisEngineeringConfig config)
        => config.Unit == MotionUnit.Pulse
            ? value
            : value * config.PulsesPerUnit / 1_000_000d;

    private static double ApplyDirection(double value, AxisEngineeringConfig config)
        => config.DirectionReversed ? -value : value;
}
