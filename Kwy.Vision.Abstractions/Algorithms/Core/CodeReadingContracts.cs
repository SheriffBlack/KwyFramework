using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public sealed record BarcodeReadRequest(
    IVisionImage Image,
    IReadOnlyList<string>? CodeTypes = null,
    IVisionRegion? SearchRegion = null,
    int MaximumCount = int.MaxValue,
    int? TimeoutMilliseconds = null,
    double? MinimumContrast = null,
    CodePolarity Polarity = CodePolarity.Any,
    bool EnableOverlay = true);

public enum CodePolarity
{
    Any,
    DarkOnLight,
    LightOnDark
}

public sealed record VisionCodeRead(
    string Text,
    string CodeType,
    VisionContour? Contour = null);

public sealed record BarcodeReadResult(IReadOnlyList<VisionCodeRead> Codes);

public sealed record DataCode2DReadRequest(
    IVisionImage Image,
    string SymbolType = "Data Matrix ECC 200",
    IVisionRegion? SearchRegion = null,
    int MaximumCount = int.MaxValue,
    int? TimeoutMilliseconds = null,
    double? MinimumContrast = null,
    CodePolarity Polarity = CodePolarity.Any,
    bool EnableOverlay = true);

public sealed record DataCode2DReadResult(IReadOnlyList<VisionCodeRead> Codes);
