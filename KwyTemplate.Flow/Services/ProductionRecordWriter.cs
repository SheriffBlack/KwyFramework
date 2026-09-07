using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Kwy.Files;

namespace KwyTemplate.Flow.Services;

public interface IProductionRecordWriter
{
    bool TryEnqueue(ProductionRecordWriteRequest request);

    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    ValueTask<bool> MoveAsync(
        string sourceDirectory,
        string sourceFileName,
        string targetDirectory,
        string? targetFileName = null,
        CancellationToken cancellationToken = default);
}

public sealed record ProductionRecordWriteRequest(
    string Directory,
    string FileName,
    IReadOnlyList<string> FieldsWithoutSequence);

public static class ProductionRecordPathHelper
{
    public static string RuntimeDirectory => Path.Combine(AppContext.BaseDirectory, "Runtime");

    public static string BuildFileName(string? workOrderNo, string fallbackName)
    {
        string value = string.IsNullOrWhiteSpace(workOrderNo) ? fallbackName : workOrderNo;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        string fileName = new(chars);
        return (string.IsNullOrWhiteSpace(fileName) ? fallbackName : fileName) + ".txt";
    }
}

/// <summary>
/// 生产记录异步写入器：生产线程只入队，后台单消费者按顺序追加到运行文件。
/// 正式保存时先 Flush，再把运行文件移动到 MES 输出目录。
/// 如果正式文件已存在，会先归档旧文件，确保 {WorkOrderNo}.txt 只保存本次数据。
/// </summary>
public sealed class ProductionRecordWriter : IProductionRecordWriter, IDisposable
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(3);
    private readonly Channel<ProductionRecordWriteMessage> queue = Channel.CreateUnbounded<ProductionRecordWriteMessage>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<string, int> nextSequences = new(StringComparer.OrdinalIgnoreCase);
    private readonly Task worker;
    private bool disposed;

    public ProductionRecordWriter()
    {
        worker = Task.Run(ProcessQueueAsync);
    }

    public bool TryEnqueue(ProductionRecordWriteRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        return queue.Writer.TryWrite(new ProductionRecordWriteMessage(request));
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.Writer.TryWrite(new ProductionRecordWriteMessage(completion)))
        {
            return;
        }

        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> MoveAsync(
        string sourceDirectory,
        string sourceFileName,
        string targetDirectory,
        string? targetFileName = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        await FlushAsync(cancellationToken).ConfigureAwait(false);

        string sourcePath = Path.Combine(sourceDirectory, sourceFileName);
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        string actualTargetFileName = string.IsNullOrWhiteSpace(targetFileName) ? sourceFileName : targetFileName;
        Directory.CreateDirectory(targetDirectory);
        string targetPath = Path.Combine(targetDirectory, actualTargetFileName);
        if (File.Exists(targetPath))
        {
            ArchiveExistingTargetFile(targetPath, targetDirectory);
        }

        File.Move(sourcePath, targetPath);
        nextSequences.TryRemove(sourcePath, out _);
        nextSequences.TryRemove(targetPath, out _);
        return true;
    }

    private static void ArchiveExistingTargetFile(string targetPath, string targetDirectory)
    {
        string archiveDirectory = Path.Combine(targetDirectory, "Archive");
        Directory.CreateDirectory(archiveDirectory);

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
        string extension = Path.GetExtension(targetPath);
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string archivePath = Path.Combine(archiveDirectory, $"{fileNameWithoutExtension}_{timestamp}{extension}");

        int retryIndex = 1;
        while (File.Exists(archivePath))
        {
            archivePath = Path.Combine(archiveDirectory, $"{fileNameWithoutExtension}_{timestamp}_{retryIndex}{extension}");
            retryIndex++;
        }

        File.Move(targetPath, archivePath);
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (ProductionRecordWriteMessage message in queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (message.FlushCompletion != null)
            {
                message.FlushCompletion.TrySetResult();
                continue;
            }

            if (message.Request == null)
            {
                continue;
            }

            try
            {
                await WriteAsync(message.Request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProductionRecordWriter] Write failed: {ex}");
            }
        }
    }

    private async Task WriteAsync(ProductionRecordWriteRequest request)
    {
        string filePath = Path.Combine(request.Directory, request.FileName);
        int sequence = nextSequences.AddOrUpdate(
            filePath,
            static path => GetNextOutputSequence(path),
            static (_, current) => current);

        string line = string.Join(",",
            new[] { sequence.ToString(CultureInfo.InvariantCulture) }.Concat(request.FieldsWithoutSequence));

        await TextFileHelper.AppendAsync(request.Directory, request.FileName, line).ConfigureAwait(false);
        nextSequences[filePath] = sequence + 1;
    }

    private static int GetNextOutputSequence(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 1;
        }

        int dataLineCount = 0;
        foreach (string line in File.ReadLines(filePath))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                dataLineCount++;
            }
        }

        return dataLineCount + 1;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        queue.Writer.TryComplete();
        try
        {
            if (!worker.Wait(DisposeTimeout))
            {
                Debug.WriteLine($"[ProductionRecordWriter] Dispose timed out after {DisposeTimeout.TotalSeconds:0.#}s.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProductionRecordWriter] Dispose failed: {ex}");
        }
    }

    private sealed record ProductionRecordWriteMessage(
        ProductionRecordWriteRequest? Request,
        TaskCompletionSource? FlushCompletion = null)
    {
        public ProductionRecordWriteMessage(ProductionRecordWriteRequest request)
            : this(request, null)
        {
        }

        public ProductionRecordWriteMessage(TaskCompletionSource flushCompletion)
            : this(null, flushCompletion)
        {
        }
    }
}