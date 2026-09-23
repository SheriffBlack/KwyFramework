using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>运动状态监视器配置，定义采样周期、受监视轴和首帧事件行为。</summary>
public sealed class MotionStateMonitorOptions
{
    /// <summary>非实时状态轮询周期；仅用于监视、诊断与动作完成判断。</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>未单独指定轴集合时的起始物理轴通道。</summary>
    public short FirstAxis { get; set; } = 1;

    /// <summary>未单独指定轴集合时连续监视的轴数量。</summary>
    public short AxisCount { get; set; } = 1;

    /// <summary>需要监视的物理轴通道；设置后优先于起始通道和数量。</summary>
    public IReadOnlyCollection<short>? Axes { get; set; }

    /// <summary>是否把首次采集的快照也发布为状态变化事件。</summary>
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
