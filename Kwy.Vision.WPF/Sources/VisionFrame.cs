using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.WPF.Sources;

public sealed record VisionFrame(
    IVisionImage Image,
    string SourceName,
    int Index,
    int? Count)
{
    public string PositionText => Count is > 0 ? $"{Index + 1}/{Count}" : $"{Index + 1}";
}
