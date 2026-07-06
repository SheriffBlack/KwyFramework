namespace Kwy.Core.Threading;

public interface IPeriodicTaskScheduler
{
    IPeriodicTaskHandle Start(
        string name,
        TimeSpan interval,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions? options = null,
        CancellationToken cancellationToken = default);
}
