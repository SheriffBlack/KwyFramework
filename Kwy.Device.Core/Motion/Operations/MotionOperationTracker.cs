using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>线程安全的运动命令追溯器。</summary>
public sealed class MotionOperationTracker : IMotionOperationTracker
{
    private readonly ConcurrentDictionary<Guid, MotionOperationSnapshot> active = new();
    public event Action<MotionOperationSnapshot>? OperationChanged;
    public IReadOnlyCollection<MotionOperationSnapshot> ActiveOperations => active.Values.ToArray();
    public MotionOperationSnapshot Start(string resourceId, MotionOperationTarget? target = null)
    {
        var snapshot = new MotionOperationSnapshot(Guid.NewGuid(), resourceId, DateTimeOffset.UtcNow, MotionOperationState.Running, target);
        active[snapshot.OperationId] = snapshot; Publish(snapshot); return snapshot;
    }
    public void Complete(MotionOperationSnapshot operation, MotionOperationState state, Exception? fault = null, string? cancellationReason = null)
    {
        MotionOperationSnapshot result = operation with { State = state, Fault = fault, CancellationReason = cancellationReason };
        active.TryRemove(operation.OperationId, out _); Publish(result);
    }
    private void Publish(MotionOperationSnapshot snapshot)
    {
        foreach (Action<MotionOperationSnapshot> subscriber in OperationChanged?.GetInvocationList().Cast<Action<MotionOperationSnapshot>>() ?? [])
            try { subscriber(snapshot); } catch { }
    }
}
