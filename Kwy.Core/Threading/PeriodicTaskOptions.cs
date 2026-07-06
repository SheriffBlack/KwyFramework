namespace Kwy.Core.Threading;

public sealed class PeriodicTaskOptions
{
    public static PeriodicTaskOptions Default { get; } = new();

    public PeriodicTaskMode Mode { get; init; } = PeriodicTaskMode.FixedDelay;

    public PeriodicTaskExceptionPolicy ExceptionPolicy { get; init; } = PeriodicTaskExceptionPolicy.Continue;

    public bool RunImmediately { get; init; } = true;

    /// <summary>
    /// Runs the polling loop on a dedicated long-running thread instead of the thread pool.
    /// This is intended only for synchronous blocking SDK polling or low-jitter fallback polling.
    /// </summary>
    public bool UseDedicatedThread { get; init; }

    /// <summary>
    /// Gets the priority of the dedicated thread when <see cref="UseDedicatedThread"/> is true.
    /// </summary>
    public ThreadPriority DedicatedThreadPriority { get; init; } = ThreadPriority.Normal;

    /// <summary>
    /// Gets whether the dedicated thread is a background thread.
    /// </summary>
    public bool DedicatedThreadIsBackground { get; init; } = true;

    public TimeSpan? ExecutionTimeout { get; init; }

    public Action<PeriodicTaskErrorContext>? OnError { get; init; }

    public Action<PeriodicTaskTickContext>? OnTickCompleted { get; init; }
}
