namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 轴快照更改的事件数据。
/// </summary>
public sealed class MotionAxisSnapshotChangedEventArgs : EventArgs
{
    public MotionAxisSnapshotChangedEventArgs(MotionAxisSnapshot snapshot, MotionAxisSnapshot? previousSnapshot)
    {
        Snapshot = snapshot;
        PreviousSnapshot = previousSnapshot;
    }

    public MotionAxisSnapshot Snapshot { get; }

    public MotionAxisSnapshot? PreviousSnapshot { get; }
}