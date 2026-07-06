using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;

namespace KwyTemplate.Vision.Batch;

public sealed class VisionBatchRunRecord
{
    public int Index { get; init; }

    public int Count { get; init; }

    public string SourceName { get; init; } = string.Empty;

    public IVisionImage? Image { get; init; }

    public IReadOnlyList<IVisionOverlayShape> Overlays { get; init; } = Array.Empty<IVisionOverlayShape>();

    public string GraphName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public double ElapsedMs { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ResultSummary { get; init; } = string.Empty;

    public string PositionText => Count > 0 ? $"{Index + 1}/{Count}" : string.Empty;

    public string ElapsedText => $"{ElapsedMs:F0} ms";
}
