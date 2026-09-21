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

    /// <summary>
    /// 仿真轴直接复用真实设备的通用轴定义。
    /// </summary>
    public IDictionary<short, AxisDefinition> Axes { get; }
        = new Dictionary<short, AxisDefinition>();

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
            foreach ((short channel, AxisDefinition definition) in Axes)
            {
                definition.Validate();
                if (channel != definition.Channel || !string.Equals(definition.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }

        return Axes.Keys.All(axis => axis >= 1 && axis <= AxisCount)
            && Axes.Values.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Axes.Count;
    }

    public AxisDefinition GetAxisDefinition(short axis)
    {
        if (axis < 1 || axis > AxisCount)
            throw new ArgumentOutOfRangeException(nameof(axis), axis, $"Axis must be between 1 and {AxisCount}.");

        return Axes.TryGetValue(axis, out AxisDefinition? definition)
            ? definition
            : new AxisDefinition
            {
                Id = $"{DeviceId}.axis.{axis}",
                DisplayName = $"Axis {axis}",
                DeviceId = DeviceId,
                Channel = axis,
                Engineering = new AxisEngineeringConfig()
            };
    }
}
