namespace Kwy.Core.Threading;

internal sealed class PeriodicTaskHandle : IPeriodicTaskHandle
{
    private CancellationTokenSource? cancellation;
    private Task? completion;
    private Thread? dedicatedThread;
    private bool isRunning;

    public PeriodicTaskHandle(string name, TimeSpan interval)
    {
        Name = name;
        Interval = interval;
    }

    public string Name { get; }

    public TimeSpan Interval { get; }

    public bool IsRunning => isRunning;

    public long TickCount { get; private set; }

    public DateTimeOffset? LastStartedAt { get; private set; }

    public DateTimeOffset? LastCompletedAt { get; private set; }

    public TimeSpan LastExecutionTime { get; private set; }

    public TimeSpan LastCycleTime { get; private set; }

    public TimeSpan LastDrift { get; private set; }

    public Exception? LastException { get; private set; }

    public Task Completion => completion ?? Task.CompletedTask;

    public void AttachCancellation(CancellationTokenSource cancellationTokenSource)
    {
        cancellation = cancellationTokenSource;
    }

    public void AttachCompletion(Task task)
    {
        completion = task;
    }

    public void AttachDedicatedThread(Thread thread, Task completionTask)
    {
        dedicatedThread = thread;
        completion = completionTask;
    }

    public void MarkRunning()
    {
        isRunning = true;
    }

    public void MarkStopped()
    {
        isRunning = false;
    }

    public void MarkTickStarted()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (LastStartedAt is not null)
        {
            LastCycleTime = now - LastStartedAt.Value;
        }

        TickCount++;
        LastStartedAt = now;
    }

    public void MarkTickCompleted(TimeSpan executionTime, TimeSpan drift)
    {
        LastExecutionTime = executionTime;
        LastDrift = drift;
        LastCompletedAt = DateTimeOffset.Now;
    }

    public void MarkError(Exception exception)
    {
        LastException = exception;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (cancellation is not null && !cancellation.IsCancellationRequested)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        cancellation?.Dispose();
    }
}
