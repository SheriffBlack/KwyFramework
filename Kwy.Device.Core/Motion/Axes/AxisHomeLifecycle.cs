using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 维护业务轴的原点可信度，而非简单复述控制器回零状态。
/// 控制器重连、伺服失能或外部移动后，自动定位必须重新取得有效原点。
/// </summary>
public sealed class AxisHomeLifecycle : IAxisHomeLifecycle
{
    private readonly ConcurrentDictionary<string, AxisHomeLifecycleSnapshot> states = new(StringComparer.OrdinalIgnoreCase);

    public event Action<AxisHomeLifecycleSnapshot>? Changed;

    public AxisHomeLifecycleSnapshot Get(string axisId) => states.TryGetValue(axisId, out var state) ? state : new(axisId, AxisHomeValidity.Unknown, DateTimeOffset.UtcNow);

    public void Begin(string axisId) => Set(axisId, AxisHomeValidity.Homing);

    public void Complete(string axisId, bool succeeded, string? detail = null) => Set(axisId, succeeded ? AxisHomeValidity.Valid : AxisHomeValidity.Failed, null, detail);

    public void Invalidate(string axisId, AxisHomeInvalidationReason reason, string? detail = null) => Set(axisId, AxisHomeValidity.Invalidated, reason, detail);

    private void Set(string axisId, AxisHomeValidity validity, AxisHomeInvalidationReason? reason = null, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisId);
        var snapshot = new AxisHomeLifecycleSnapshot(axisId, validity, DateTimeOffset.UtcNow, reason, detail);
        states[axisId] = snapshot;
        foreach (Action<AxisHomeLifecycleSnapshot> handler in Changed?.GetInvocationList().Cast<Action<AxisHomeLifecycleSnapshot>>() ?? []) try { handler(snapshot); } catch { }
    }
}
