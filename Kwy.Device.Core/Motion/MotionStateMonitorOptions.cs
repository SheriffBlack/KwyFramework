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
        if (Axes is not null)
        {
            if (Axes.Count == 0)
                throw new InvalidOperationException("At least one monitored axis must be configured.");
            return Axes.ToArray();
        }

        if (AxisCount < 1)
        {
            throw new InvalidOperationException("AxisCount must be greater than or equal to 1.");
        }

        if (FirstAxis < 1)
        {
            throw new InvalidOperationException("FirstAxis must be greater than or equal to 1.");
        }

        if ((int)FirstAxis + AxisCount - 1 > short.MaxValue)
            throw new InvalidOperationException("The configured axis range exceeds Int16.MaxValue.");

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

        IReadOnlyCollection<short> axes = GetAxes();
        if (axes.Any(static axis => axis < 1))
            throw new InvalidOperationException("Monitored axes must be greater than or equal to 1.");
        if (axes.Distinct().Count() != axes.Count)
            throw new InvalidOperationException("Monitored axes cannot contain duplicates.");
    }
}
