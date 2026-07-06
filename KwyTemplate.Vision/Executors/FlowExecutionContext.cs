using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.Services;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;

namespace KwyTemplate.Vision.Executors;

/// <summary>
/// 单次流程运行上下文。后续相机、视觉库、设备服务、日志服务都应从这里进入执行器。
/// </summary>
public sealed class FlowExecutionContext
{
    public const string BatchCurrentImageKey = "Vision.Batch.CurrentImage";
    public const string BatchCurrentSourceNameKey = "Vision.Batch.CurrentSourceName";

    public FlowExecutionContext(
        FlowGraph graph,
        bool isDebug,
        CancellationToken cancellationToken)
    {
        Graph = graph;
        IsDebug = isDebug;
        CancellationToken = cancellationToken;
    }

    public FlowGraph Graph { get; }

    public bool IsDebug { get; }

    public CancellationToken CancellationToken { get; }

    public DateTime StartedAt { get; } = DateTime.Now;

    public Dictionary<string, object?> Items { get; } = new();

    public List<FlowPortValueSnapshot> Variables { get; } = new();

    public List<FlowImageSnapshot> Images { get; } = new();

    public void RecordPortValue(
        string nodeId,
        string nodeName,
        string portName,
        PortDirection direction,
        string dataType,
        FlowValue value)
    {
        Variables.Add(new FlowPortValueSnapshot(
            nodeId,
            nodeName,
            portName,
            direction,
            dataType,
            value.HasValue,
            value.Value,
            FormatValue(value)));
    }

    public void RecordImage(
        string nodeId,
        string nodeName,
        string portName,
        PortDirection direction,
        IVisionImage image,
        IReadOnlyList<IVisionOverlayShape>? overlays = null,
        int? sequenceIndex = null,
        int? sequenceCount = null)
    {
        Images.Add(new FlowImageSnapshot(
            nodeId,
            nodeName,
            portName,
            direction,
            image,
            overlays ?? Array.Empty<IVisionOverlayShape>(),
            sequenceIndex,
            sequenceCount));
    }

    private static string FormatValue(FlowValue value)
        => FlowValueDisplayFormatter.FormatFlowValue(value);
}

public sealed record FlowPortValueSnapshot(
    string NodeId,
    string NodeName,
    string PortName,
    PortDirection Direction,
    string DataType,
    bool HasValue,
    object? Value,
    string DisplayValue);

public sealed record FlowImageSnapshot(
    string NodeId,
    string NodeName,
    string PortName,
    PortDirection Direction,
    IVisionImage Image,
    IReadOnlyList<IVisionOverlayShape> Overlays,
    int? SequenceIndex = null,
    int? SequenceCount = null)
{
    public string Summary => $"{Image.Width}x{Image.Height}, {Image.PixelFormat}, {Image.BackendId}";
}
