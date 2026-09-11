using System.Diagnostics;
using System.Threading.Channels;

namespace KwyTemplate.Vision.Batch;

public sealed class VisionBatchProcessor : IAsyncDisposable
{
    private readonly Func<VisionBatchItem, CancellationToken, ValueTask<object?>> processor;
    private readonly List<Task> workers = new();
    private CancellationTokenSource? cancellationTokenSource;
    private Channel<VisionBatchItem>? queue;

    public VisionBatchProcessor(Func<VisionBatchItem, CancellationToken, ValueTask<object?>> processor)
    {
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public int WorkerCount { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    public int QueueCapacity { get; set; } = 100;

    public bool IsRunning { get; private set; }

    public int CompletedCount { get; private set; }

    public int FailedCount { get; private set; }

    public int QueueLength => queue?.Reader.Count ?? 0;

    public event EventHandler<VisionBatchItem>? ItemCompleted;

    public event EventHandler? ProcessingCompleted;

    public async Task StartAsync(IEnumerable<string> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (IsRunning)
        {
            await StopAsync().ConfigureAwait(false);
        }

        CompletedCount = 0;
        FailedCount = 0;
        UnsafeCompletedCount = 0;
        UnsafeFailedCount = 0;
        IsRunning = true;
        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        queue = Channel.CreateBounded<VisionBatchItem>(new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true
        });

        workers.Clear();
        for (int i = 0; i < WorkerCount; i++)
        {
            workers.Add(Task.Run(() => WorkerAsync(cancellationTokenSource.Token), CancellationToken.None));
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    foreach (string source in sources)
                    {
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                        await queue.Writer
                            .WriteAsync(new VisionBatchItem(source), cancellationTokenSource.Token)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    queue.Writer.TryComplete();
                }
            },
            CancellationToken.None);
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        cancellationTokenSource?.Cancel();
        if (queue != null)
        {
            queue.Writer.TryComplete();
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        workers.Clear();
        IsRunning = false;
        ProcessingCompleted?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        Channel<VisionBatchItem> currentQueue = queue ?? throw new InvalidOperationException("Batch queue is not initialized.");
        await foreach (VisionBatchItem item in currentQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                item.Status = VisionBatchItemStatus.Processing;
                item.Result = await processor(item, cancellationToken).ConfigureAwait(false);
                item.Status = VisionBatchItemStatus.Completed;
                Interlocked.Increment(ref UnsafeCompletedCount);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                item.Status = VisionBatchItemStatus.Canceled;
                throw;
            }
            catch (Exception ex)
            {
                item.Status = VisionBatchItemStatus.Failed;
                item.ErrorMessage = ex.Message;
                Interlocked.Increment(ref UnsafeFailedCount);
            }
            finally
            {
                stopwatch.Stop();
                item.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                CompletedCount = UnsafeCompletedCount;
                FailedCount = UnsafeFailedCount;
                ItemCompleted?.Invoke(this, item);
            }
        }
    }

    private int UnsafeCompletedCount;

    private int UnsafeFailedCount;
}
