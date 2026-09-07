using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.Algorithms;

public enum VisionPreprocessOperation
{
    Mean,
    Median,
    Gaussian,
    Emphasize,
    GrayOpening,
    GrayClosing,
    GrayDilation,
    GrayErosion,
    ScaleGray,
    GrayOpeningCircle,
    GrayClosingCircle,
    GrayDilationCircle,
    GrayErosionCircle,
    AnisotropicDiffusion,
    EqualizeHistogram,
    Illuminate
}

public sealed record ImagePreprocessRequest(
    IVisionImage Image,
    VisionPreprocessOperation Operation,
    int MaskWidth = 3,
    int MaskHeight = 3,
    double Factor = 1.0,
    double Offset = 0,
    double Radius = 3,
    double Theta = 10,
    int Iterations = 10,
    string Mode = "weickert",
    IVisionRegion? Region = null);

public sealed record ImagePreprocessResult(
    IVisionImage Image);
