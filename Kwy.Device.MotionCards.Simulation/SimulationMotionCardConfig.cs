using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.MotionCards.Simulation;

public sealed class SimulationMotionCardConfig : IDeviceConfig
{
    public string DeviceId { get; set; } = "SimulationMotion";

    public string DeviceName { get; set; } = "Simulation Motion Card";

    public short AxisCount { get; set; } = 4;

    public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    public double SimulationSpeedRatio { get; set; } = 1;

    public IList<AxisEngineeringConfig> AxisEngineeringConfigs { get; } = new List<AxisEngineeringConfig>();

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

        try
        {
            foreach (AxisEngineeringConfig item in AxisEngineeringConfigs)
            {
                item.Validate();
            }
        }
        catch (ArgumentException)
        {
            return false;
        }

        return AxisEngineeringConfigs.All(item => item.Axis <= AxisCount)
            && AxisEngineeringConfigs.Select(item => item.Axis).Distinct().Count() == AxisEngineeringConfigs.Count;
    }

    public AxisEngineeringConfig GetAxisEngineeringConfig(short axis)
        => AxisEngineeringConfigs.FirstOrDefault(item => item.Axis == axis)
            ?? new AxisEngineeringConfig { Axis = axis };
}
