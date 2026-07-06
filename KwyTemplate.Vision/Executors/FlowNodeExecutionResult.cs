using Kwy.Vision.Abstractions.Results;

namespace KwyTemplate.Vision.Executors;

/// <summary>
/// Result of a single flow node execution.
/// </summary>
public sealed class FlowNodeExecutionResult
{
    private FlowNodeExecutionResult(
        bool success,
        string? errorMessage,
        IReadOnlyDictionary<string, FlowValue> outputs,
        IReadOnlyList<IVisionOverlayShape> overlays)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Outputs = outputs;
        Overlays = overlays;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyDictionary<string, FlowValue> Outputs { get; }

    public IReadOnlyList<IVisionOverlayShape> Overlays { get; }

    public static FlowNodeExecutionResult Ok(
        IDictionary<string, FlowValue>? outputs = null,
        IReadOnlyList<IVisionOverlayShape>? overlays = null)
        => new(
            true,
            null,
            new Dictionary<string, FlowValue>(outputs ?? new Dictionary<string, FlowValue>()),
            overlays ?? Array.Empty<IVisionOverlayShape>());

    public static FlowNodeExecutionResult OkObjects(
        IDictionary<string, object?> outputs,
        IReadOnlyList<IVisionOverlayShape>? overlays = null)
        => Ok(outputs.ToDictionary(item => item.Key, item => FlowValue.From(item.Value)), overlays);

    public static FlowNodeExecutionResult Failed(string errorMessage)
        => new(false, errorMessage, new Dictionary<string, FlowValue>(), Array.Empty<IVisionOverlayShape>());
}
