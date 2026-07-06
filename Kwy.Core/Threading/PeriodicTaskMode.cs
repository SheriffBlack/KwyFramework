namespace Kwy.Core.Threading;

public enum PeriodicTaskMode
{
    /// <summary>
    /// Waits for the interval after each execution. It is simple and best for low-frequency work.
    /// </summary>
    FixedDelay = 0,

    /// <summary>
    /// Uses Stopwatch to keep a fixed schedule and reduce accumulated drift.
    /// </summary>
    FixedRate = 1,

    /// <summary>
    /// Uses PeriodicTimer. It is the default async-friendly timer for normal periodic tasks.
    /// </summary>
    PeriodicTimer = 2
}
