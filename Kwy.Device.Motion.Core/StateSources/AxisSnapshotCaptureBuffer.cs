using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;

namespace Kwy.Device.Motion.Core.StateSources;

/// <summary>复用轴批量读取策略与缓冲区，避免各采集源重复实现 SDK 降级逻辑。</summary>
internal sealed class AxisSnapshotCaptureBuffer
{
    private readonly IAxisSnapshotReader snapshotReader;
    private readonly short[] axes;
    private readonly MotionAxisSnapshot[] snapshots;

    public AxisSnapshotCaptureBuffer(IAxisSnapshotReader snapshotReader, IReadOnlyCollection<short> axes)
    {
        this.snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        ArgumentNullException.ThrowIfNull(axes);
        this.axes = axes.ToArray();
        snapshots = new MotionAxisSnapshot[this.axes.Length];
    }

    public void Capture(Action<MotionAxisSnapshot> publish)
    {
        ArgumentNullException.ThrowIfNull(publish);

        if (snapshotReader is IBufferedAxisSnapshotReader bufferedReader)
        {
            bufferedReader.GetMultipleAxisSnapshots(axes, snapshots);
        }
        else if (snapshotReader is IBulkAxisSnapshotReader bulkReader)
        {
            MotionAxisSnapshot[] captured = bulkReader.GetMultipleAxisSnapshots(axes);
            if (captured.Length != axes.Length)
            {
                throw new InvalidOperationException($"Snapshot reader returned {captured.Length} axes; expected {axes.Length}.");
            }

            captured.CopyTo(snapshots, 0);
        }
        else
        {
            for (int index = 0; index < axes.Length; index++)
            {
                snapshots[index] = snapshotReader.GetAxisSnapshot(axes[index]);
            }
        }

        for (int index = 0; index < axes.Length; index++)
        {
            MotionAxisSnapshot snapshot = snapshots[index];
            if (snapshot.Axis != axes[index])
            {
                throw new InvalidOperationException(
                    $"Snapshot index {index} returned axis {snapshot.Axis}; expected axis {axes[index]}.");
            }

            publish(snapshot);
        }
    }
}
