using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.Halcon;

/// <summary>
/// Base class for HALCON traditional-vision algorithms. Concrete implementations may keep
/// HImage/HObject instances internally, but their public requests and results remain backend-neutral.
/// </summary>
public abstract class HalconVisionAlgorithm<TRequest, TResult> 
    : VisionAlgorithmBase<TRequest, TResult>
{
    protected HalconVisionAlgorithm(string algorithmId)
        : base(algorithmId, VisionBackendIds.Halcon)
    {
    }
}
