using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

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
