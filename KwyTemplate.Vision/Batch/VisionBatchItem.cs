namespace KwyTemplate.Vision.Batch;

public sealed class VisionBatchItem
{
    public VisionBatchItem(string source)
    {
        Source = string.IsNullOrWhiteSpace(source)
            ? throw new ArgumentException("Source cannot be empty.", nameof(source))
            : source;
    }

    public string Source { get; }

    public VisionBatchItemStatus Status { get; internal set; } = VisionBatchItemStatus.Pending;

    public long ElapsedMilliseconds { get; internal set; }

    public string? ErrorMessage { get; internal set; }

    public object? Result { get; internal set; }
}
