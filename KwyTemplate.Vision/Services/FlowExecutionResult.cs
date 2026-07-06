using KwyTemplate.Vision.ViewModels.Items;
using KwyTemplate.Vision.Executors;

namespace KwyTemplate.Vision.Services;

public enum FlowExecutionStatus
{
    Completed,
    Cancelled,
    Failed
}

/// <summary>
/// 一次流程运行的汇总结果。
/// </summary>
public sealed class FlowExecutionResult
{
    public FlowExecutionStatus Status { get; init; }

    public int ExecutedCount { get; init; }

    public FlowNodeViewModel? ErrorNode { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Elapsed { get; init; }

    public IReadOnlyList<FlowNodeRunRecord> NodeRecords { get; init; } = Array.Empty<FlowNodeRunRecord>();

    public IReadOnlyList<FlowPortValueSnapshot> Variables { get; init; } = Array.Empty<FlowPortValueSnapshot>();

    public IReadOnlyList<FlowImageSnapshot> Images { get; init; } = Array.Empty<FlowImageSnapshot>();

    public static FlowExecutionResult Completed(
        int executedCount,
        TimeSpan elapsed,
        IReadOnlyList<FlowNodeRunRecord>? nodeRecords = null,
        IReadOnlyList<FlowPortValueSnapshot>? variables = null,
        IReadOnlyList<FlowImageSnapshot>? images = null)
        => new()
        {
            Status = FlowExecutionStatus.Completed,
            ExecutedCount = executedCount,
            Elapsed = elapsed,
            NodeRecords = nodeRecords ?? Array.Empty<FlowNodeRunRecord>(),
            Variables = variables ?? Array.Empty<FlowPortValueSnapshot>(),
            Images = images ?? Array.Empty<FlowImageSnapshot>()
        };

    public static FlowExecutionResult Cancelled(
        int executedCount,
        TimeSpan elapsed,
        IReadOnlyList<FlowNodeRunRecord>? nodeRecords = null,
        IReadOnlyList<FlowPortValueSnapshot>? variables = null,
        IReadOnlyList<FlowImageSnapshot>? images = null)
        => new()
        {
            Status = FlowExecutionStatus.Cancelled,
            ExecutedCount = executedCount,
            Elapsed = elapsed,
            NodeRecords = nodeRecords ?? Array.Empty<FlowNodeRunRecord>(),
            Variables = variables ?? Array.Empty<FlowPortValueSnapshot>(),
            Images = images ?? Array.Empty<FlowImageSnapshot>()
        };

    public static FlowExecutionResult Failed(
        FlowNodeViewModel? node,
        string errorMessage,
        int executedCount,
        TimeSpan elapsed,
        IReadOnlyList<FlowNodeRunRecord>? nodeRecords = null,
        IReadOnlyList<FlowPortValueSnapshot>? variables = null,
        IReadOnlyList<FlowImageSnapshot>? images = null)
        => new()
        {
            Status = FlowExecutionStatus.Failed,
            ErrorNode = node,
            ErrorMessage = errorMessage,
            ExecutedCount = executedCount,
            Elapsed = elapsed,
            NodeRecords = nodeRecords ?? Array.Empty<FlowNodeRunRecord>(),
            Variables = variables ?? Array.Empty<FlowPortValueSnapshot>(),
            Images = images ?? Array.Empty<FlowImageSnapshot>()
        };
}

public sealed class FlowNodeRunRecord
{
    public string NodeId { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public string NodeType { get; init; } = string.Empty;

    public bool Success { get; init; }

    public TimeSpan Elapsed { get; init; }

    public string? Message { get; init; }

    public string StatusText => Success ? "OK" : "NG";

    public string ElapsedText => $"{Elapsed.TotalMilliseconds:F0} ms";
}
