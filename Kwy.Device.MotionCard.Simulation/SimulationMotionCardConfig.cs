using Kwy.Device.Abstractions;
using Kwy.Device.Motion.Abstractions;

namespace Kwy.Device.MotionCard.Simulation;

public sealed class SimulationMotionCardConfig : IDeviceConfig
{
    public string DeviceId { get; set; } = "SimulationMotion";

    public string DeviceName { get; set; } = "Simulation Motion Card";

    public short AxisCount { get; set; } = 4;

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    public double SimulationSpeedRatio { get; set; } = 1;

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(DeviceId)
            || string.IsNullOrWhiteSpace(DeviceName)
            || AxisCount < 1
            || UpdateInterval <= TimeSpan.Zero
            || !double.IsFinite(SimulationSpeedRatio)
            || SimulationSpeedRatio <= 0)
        {
            return false;
        }

        return true;
    }
}
