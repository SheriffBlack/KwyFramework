namespace Kwy.Core.Threading;

public sealed record BackgroundTaskError(
    string Source,
    Exception Exception,
    DateTimeOffset Time,
    long? TickCount = null);
