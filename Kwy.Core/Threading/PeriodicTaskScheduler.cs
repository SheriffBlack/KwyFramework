using System.Diagnostics;

namespace Kwy.Core.Threading;

public sealed class PeriodicTaskScheduler : IPeriodicTaskScheduler
{
    private readonly IBackgroundTaskErrorReporter? errorReporter;

    public PeriodicTaskScheduler()
    {
    }

    public PeriodicTaskScheduler(IBackgroundTaskErrorReporter errorReporter)
    {
        this.errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    public IPeriodicTaskHandle Start(
        string name,
        TimeSpan interval,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be empty.", nameof(name));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(action);

        var handle = new PeriodicTaskHandle(name, interval);
        options ??= PeriodicTaskOptions.Default;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handle.AttachCancellation(linkedCts);

        if (options.UseDedicatedThread)
        {
            StartDedicatedThread(handle, action, options, linkedCts.Token);
            return handle;
        }

        Task task = Task.Run(
            () => RunAsync(handle, action, options, errorReporter, linkedCts.Token),
            CancellationToken.None);
        handle.AttachCompletion(task);
        return handle;
    }

    private void StartDedicatedThread(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                RunDedicatedThread(handle, action, options, errorReporter, cancellationToken);
                completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = options.DedicatedThreadIsBackground,
            Name = $"Kwy_Periodic_{handle.Name}",
            Priority = options.DedicatedThreadPriority
        };

        handle.AttachDedicatedThread(thread, completion.Task);
        thread.Start();
    }

    private static async Task RunAsync(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        handle.MarkRunning();

        try
        {
            if (options.RunImmediately)
            {
                await ExecuteTickAsync(handle, action, options, errorReporter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            }

            switch (options.Mode)
            {
                case PeriodicTaskMode.FixedRate:
                    await RunFixedRateAsync(handle, action, options, errorReporter, cancellationToken).ConfigureAwait(false);
                    break;

                case PeriodicTaskMode.FixedDelay:
                    await RunFixedDelayAsync(handle, action, options, errorReporter, cancellationToken).ConfigureAwait(false);
                    break;

                case PeriodicTaskMode.PeriodicTimer:
                default:
                    await RunPeriodicTimerAsync(handle, action, options, errorReporter, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            handle.MarkStopped();
        }
    }

    private static void RunDedicatedThread(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        handle.MarkRunning();

        try
        {
            if (options.RunImmediately)
            {
                ExecuteTickBlocking(handle, action, options, errorReporter, TimeSpan.Zero, cancellationToken);
            }

            if (options.Mode == PeriodicTaskMode.FixedRate)
            {
                RunFixedRateBlocking(handle, action, options, errorReporter, cancellationToken);
                return;
            }

            RunFixedDelayBlocking(handle, action, options, errorReporter, cancellationToken);
        }
        finally
        {
            handle.MarkStopped();
        }
    }

    private static void RunFixedDelayBlocking(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (cancellationToken.WaitHandle.WaitOne(handle.Interval))
            {
                break;
            }

            ExecuteTickBlocking(handle, action, options, errorReporter, TimeSpan.Zero, cancellationToken);
        }
    }

    private static void RunFixedRateBlocking(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TimeSpan nextTick = stopwatch.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            nextTick += handle.Interval;
            TimeSpan delay = nextTick - stopwatch.Elapsed;
            if (delay > TimeSpan.Zero && cancellationToken.WaitHandle.WaitOne(delay))
            {
                break;
            }

            TimeSpan drift = stopwatch.Elapsed - nextTick;
            ExecuteTickBlocking(handle, action, options, errorReporter, drift, cancellationToken);
        }
    }

    private static void ExecuteTickBlocking(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        TimeSpan drift,
        CancellationToken cancellationToken)
    {
        var tickStopwatch = Stopwatch.StartNew();
        handle.MarkTickStarted();

        using CancellationTokenSource? timeoutCts = options.ExecutionTimeout is { } timeout
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts is not null && options.ExecutionTimeout is { } executionTimeout)
        {
            timeoutCts.CancelAfter(executionTimeout);
        }

        CancellationToken tickToken = timeoutCts?.Token ?? cancellationToken;

        try
        {
            action(tickToken).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            handle.MarkError(ex);
            errorReporter?.Report(new BackgroundTaskError(
                handle.Name,
                ex,
                DateTimeOffset.Now,
                handle.TickCount));

            options.OnError?.Invoke(new PeriodicTaskErrorContext(
                handle.Name,
                handle.TickCount,
                ex,
                DateTimeOffset.Now));

            if (options.ExceptionPolicy == PeriodicTaskExceptionPolicy.Stop)
            {
                throw;
            }
        }
        finally
        {
            tickStopwatch.Stop();
            handle.MarkTickCompleted(tickStopwatch.Elapsed, drift);
            options.OnTickCompleted?.Invoke(new PeriodicTaskTickContext(
                handle.Name,
                handle.TickCount,
                tickStopwatch.Elapsed,
                handle.LastCycleTime,
                drift,
                DateTimeOffset.Now));
        }
    }

    private static async Task RunPeriodicTimerAsync(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(handle.Interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await ExecuteTickAsync(handle, action, options, errorReporter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RunFixedDelayAsync(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(handle.Interval, cancellationToken).ConfigureAwait(false);
            await ExecuteTickAsync(handle, action, options, errorReporter, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RunFixedRateAsync(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TimeSpan nextTick = stopwatch.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            nextTick += handle.Interval;
            TimeSpan delay = nextTick - stopwatch.Elapsed;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            TimeSpan drift = stopwatch.Elapsed - nextTick;
            await ExecuteTickAsync(handle, action, options, errorReporter, drift, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask ExecuteTickAsync(
        PeriodicTaskHandle handle,
        Func<CancellationToken, ValueTask> action,
        PeriodicTaskOptions options,
        IBackgroundTaskErrorReporter? errorReporter,
        TimeSpan drift,
        CancellationToken cancellationToken)
    {
        var tickStopwatch = Stopwatch.StartNew();
        handle.MarkTickStarted();

        using CancellationTokenSource? timeoutCts = options.ExecutionTimeout is { } timeout
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutCts is not null && options.ExecutionTimeout is { } executionTimeout)
        {
            timeoutCts.CancelAfter(executionTimeout);
        }

        CancellationToken tickToken = timeoutCts?.Token ?? cancellationToken;

        try
        {
            await action(tickToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            handle.MarkError(ex);
            errorReporter?.Report(new BackgroundTaskError(
                handle.Name,
                ex,
                DateTimeOffset.Now,
                handle.TickCount));

            options.OnError?.Invoke(new PeriodicTaskErrorContext(
                handle.Name,
                handle.TickCount,
                ex,
                DateTimeOffset.Now));

            if (options.ExceptionPolicy == PeriodicTaskExceptionPolicy.Stop)
            {
                throw;
            }
        }
        finally
        {
            tickStopwatch.Stop();
            handle.MarkTickCompleted(tickStopwatch.Elapsed, drift);
            options.OnTickCompleted?.Invoke(new PeriodicTaskTickContext(
                handle.Name,
                handle.TickCount,
                tickStopwatch.Elapsed,
                handle.LastCycleTime,
                drift,
                DateTimeOffset.Now));
        }
    }
}
