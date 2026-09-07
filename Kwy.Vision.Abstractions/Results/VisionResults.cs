using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Results;

public readonly record struct VisionColor(byte R, byte G, byte B, byte A = 255)
{
    public static VisionColor Red => new(255, 0, 0);
    public static VisionColor Green => new(0, 255, 0);
    public static VisionColor Blue => new(0, 0, 255);
    public static VisionColor Yellow => new(255, 255, 0);
    public static VisionColor Cyan => new(0, 255, 255);
    public static VisionColor Magenta => new(255, 0, 255);
    public static VisionColor White => new(255, 255, 255);
    public static VisionColor Black => new(0, 0, 0);
}

public interface IVisionOverlayShape
{
    VisionColor Color { get; }
    double Thickness { get; }
    string Label { get; }
}

public sealed record OverlayLine(VisionLine Line, VisionColor Color, double Thickness, string Label = "") : IVisionOverlayShape;
public sealed record OverlayCircle(VisionCircle Circle, VisionColor Color, double Thickness, string Label = "") : IVisionOverlayShape;
public sealed record OverlayContour(VisionContour Contour, VisionColor Color, double Thickness, string Label = "") : IVisionOverlayShape;
public sealed record OverlayText(VisionPoint Position, string Text, VisionColor Color, double FontSize, double Thickness = 1.0, string Label = "") : IVisionOverlayShape;
public sealed record OverlayRectangle(VisionRectangle Rectangle, VisionColor Color, double Thickness, string Label = "") : IVisionOverlayShape;

public sealed record VisionExecutionResult<T>(
    bool Succeeded,
    T? Value,
    TimeSpan Elapsed,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyDictionary<string, string>? Diagnostics = null,
    IReadOnlyList<IVisionOverlayShape>? Overlays = null)
{
    public IReadOnlyList<IVisionOverlayShape> Overlays { get; init; } = Overlays ?? Array.Empty<IVisionOverlayShape>();

    public static VisionExecutionResult<T> Success(
        T value,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, string>? diagnostics = null,
        IReadOnlyList<IVisionOverlayShape>? overlays = null)
        => new(true, value, elapsed, Diagnostics: diagnostics, Overlays: overlays);

    public static VisionExecutionResult<T> Failure(
        string errorCode,
        string errorMessage,
        TimeSpan elapsed,
        IReadOnlyDictionary<string, string>? diagnostics = null)
        => new(false, default, elapsed, errorCode, errorMessage, diagnostics);
}

public sealed record VisionMatch(
    VisionPose2D Pose,
    double Score,
    VisionRectangle? Bounds = null);

public sealed record VisionMeasurement(
    string Name,
    double Value,
    string Unit,
    bool Passed,
    double? Minimum = null,
    double? Maximum = null);

public sealed record VisionDefect(
    string Code,
    string Description,
    double Confidence,
    VisionRectangle? Bounds = null,
    IVisionRegion? Region = null);

public sealed record VisionOverlay(
    IReadOnlyList<IVisionRegion> Regions,
    IReadOnlyList<VisionPoint>? Points = null,
    IReadOnlyList<string>? Labels = null);

public sealed record VisionImageResult(IVisionImage Image);
