using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>以排序锁序获取多个业务轴，避免单轴与插补组互相抢占和死锁。</summary>
public sealed class MotionResourceLock : IMotionResourceLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IDisposable> AcquireAsync(IEnumerable<string> axisIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(axisIds);
        string[] ids = axisIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Length == 0) throw new ArgumentException("At least one axis ID is required.", nameof(axisIds));
        var acquired = new List<SemaphoreSlim>(ids.Length);
        try
        {
            foreach (string id in ids)
            {
                SemaphoreSlim gate = locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(gate);
            }
            return new Releaser(acquired);
        }
        catch { foreach (SemaphoreSlim gate in acquired) gate.Release(); throw; }
    }

    private sealed class Releaser(List<SemaphoreSlim> gates) : IDisposable
    {
        private List<SemaphoreSlim>? owned = gates;
        public void Dispose() { List<SemaphoreSlim>? current = Interlocked.Exchange(ref owned, null); if (current is not null) foreach (SemaphoreSlim gate in current) gate.Release(); }
    }
}
