using KwyTemplate.Vision.ViewModels.Items;

namespace KwyTemplate.Vision.Services;

public enum FlowRuntimeEventKind
{
    FlowStarted,
    FlowCompleted,
    FlowCancelled,
    FlowFailed,
    NodeStarted,
    NodeCompleted,
    NodeFailed,
    DebugPaused
}

public sealed class FlowRuntimeEvent
{
    public FlowRuntimeEventKind Kind { get; init; }

    public FlowNodeViewModel? Node { get; init; }

    public string Message { get; init; } = string.Empty;

    public TimeSpan? Elapsed { get; init; }
}
