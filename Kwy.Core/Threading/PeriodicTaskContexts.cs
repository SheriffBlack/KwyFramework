namespace Kwy.Core.Threading;

public sealed record PeriodicTaskErrorContext(
    string Name,
    long TickCount,
    Exception Exception,
    DateTimeOffset Time);

public sealed record PeriodicTaskTickContext(
    string Name,
    long TickCount,
    TimeSpan ExecutionTime,
    TimeSpan CycleTime,
    TimeSpan Drift,
    DateTimeOffset Time);
