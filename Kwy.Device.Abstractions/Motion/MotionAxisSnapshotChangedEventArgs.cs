namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// Event data for axis snapshot changes.
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
