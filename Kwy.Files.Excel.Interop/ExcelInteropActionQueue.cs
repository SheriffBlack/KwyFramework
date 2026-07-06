using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Kwy.Files.Excel.Abstractions;

namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropActionQueue : IExcelActionQueue, IDisposable
{
    private sealed class QueueItem
    {
        public required Action Work { get; init; }

        public required Action<Exception> OnError { get; init; }
    }

    private readonly BlockingCollection<QueueItem> queue = new();
    private readonly CancellationTokenSource destroyCts = new();
    private readonly Thread workerThread;
    private readonly ExcelInteropOptions options;
    private bool disposed;

    public ExcelInteropActionQueue(ExcelInteropOptions? options = null)
    {
        this.options = options ?? new ExcelInteropOptions();
        workerThread = new Thread(WorkLoop)
        {
            IsBackground = true,
            Name = "Kwy_Excel_Interop_STA"
        };
        workerThread.SetApartmentState(ApartmentState.STA);
        workerThread.Start();
    }

    public Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EnqueueCore(
            () =>
            {
                action();
                completion.SetResult();
            },
            ex => completion.SetException(ex),
            cancellationToken);

        return completion.Task;
    }

    public Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        EnqueueCore(
            () => completion.SetResult(action()),
            ex => completion.SetException(ex),
            cancellationToken);

        return completion.Task;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        queue.CompleteAdding();
        destroyCts.Cancel();
        if (workerThread.IsAlive && Thread.CurrentThread != workerThread)
        {
            workerThread.Join(TimeSpan.FromSeconds(3));
        }

        destroyCts.Dispose();
        queue.Dispose();
    }

    private void EnqueueCore(Action work, Action<Exception> onError, CancellationToken cancellationToken)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ExcelInteropActionQueue));
        }

        cancellationToken.ThrowIfCancellationRequested();
        queue.Add(new QueueItem { Work = work, OnError = onError }, cancellationToken);
    }

    private void WorkLoop()
    {
        try
        {
            foreach (var item in queue.GetConsumingEnumerable(destroyCts.Token))
            {
                ExecuteWithRetry(item);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ExecuteWithRetry(QueueItem item)
    {
        Exception? lastError = null;

        for (int retry = 0; retry <= options.MaxComRetries; retry++)
        {
            try
            {
                item.Work();
                return;
            }
            catch (Exception ex) when (IsRetryableComException(ex) && retry < options.MaxComRetries)
            {
                lastError = ex;
                Thread.Sleep(options.ComRetryDelay);
            }
            catch (Exception ex)
            {
                item.OnError(ex);
                return;
            }
        }

        item.OnError(lastError ?? new ExcelInteropException("Excel COM operation failed after retry."));
    }

    private static bool IsRetryableComException(Exception exception)
    {
        int errorCode = exception switch
        {
            COMException comException => comException.ErrorCode,
            { InnerException: COMException innerComException } => innerComException.ErrorCode,
            _ => 0
        };

        return unchecked((uint)errorCode) is 0x800AC472 or 0x8001010A or 0x800A03EC or 0x80010001;
    }
}
