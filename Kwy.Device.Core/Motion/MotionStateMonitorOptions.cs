using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// Options for <see cref="MotionStateMonitor"/>.
/// </summary>
public sealed class MotionStateMonitorOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    public short FirstAxis { get; set; } = 1;

    public short AxisCount { get; set; } = 1;

    public IReadOnlyCollection<short>? Axes { get; set; }

    public bool RaiseInitialSnapshotChanged { get; set; } = true;

    public IReadOnlyCollection<short> GetAxes()
    {
        if (Axes is { Count: > 0 })
        {
            return Axes;
        }

        if (AxisCount < 1)
        {
            throw new InvalidOperationException("AxisCount must be greater than or equal to 1.");
        }

        if (FirstAxis < 1)
        {
            throw new InvalidOperationException("FirstAxis must be greater than or equal to 1.");
        }

        var axes = new short[AxisCount];
        for (short i = 0; i < AxisCount; i++)
        {
            axes[i] = (short)(FirstAxis + i);
        }

        return axes;
    }

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("PollInterval must be greater than zero.");
        }

        _ = GetAxes();
    }
}
