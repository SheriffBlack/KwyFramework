using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.OpenCV;

/// <summary>
/// Base class for OpenCV traditional-vision algorithms. Mat instances remain inside this module.
/// </summary>
public abstract class OpenCvVisionAlgorithm<TRequest, TResult> : VisionAlgorithmBase<TRequest, TResult>
{
    protected OpenCvVisionAlgorithm(string algorithmId)
        : base(algorithmId, VisionBackendIds.OpenCv)
    {
    }
}
