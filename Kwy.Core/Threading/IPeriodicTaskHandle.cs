namespace Kwy.Core.Threading;

public interface IPeriodicTaskHandle : IAsyncDisposable
{
    string Name { get; }

    TimeSpan Interval { get; }

    bool IsRunning { get; }

    long TickCount { get; }

    DateTimeOffset? LastStartedAt { get; }

    DateTimeOffset? LastCompletedAt { get; }

    TimeSpan LastExecutionTime { get; }

    /// <summary>
    /// Gets the interval between the last two tick start times. It represents execution plus waiting delay.
    /// </summary>
    TimeSpan LastCycleTime { get; }

    TimeSpan LastDrift { get; }

    Exception? LastException { get; }

    Task Completion { get; }

    Task StopAsync(CancellationToken cancellationToken = default);
}
