using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.DeepLearning.Onnx;

public enum OnnxExecutionProvider
{
    Cpu,
    DirectML,
    Cuda,
    TensorRt
}

public sealed class OnnxVisionModelConfig
{
    public required string ModelId { get; init; }

    public required string ModelPath { get; init; }

    public OnnxExecutionProvider ExecutionProvider { get; init; } = OnnxExecutionProvider.Cpu;

    public int IntraOpThreadCount { get; init; }

    public int InterOpThreadCount { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelPath);
        if (IntraOpThreadCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IntraOpThreadCount));
        }

        if (InterOpThreadCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(InterOpThreadCount));
        }
    }
}

/// <summary>Base class for ONNX models. InferenceSession and tensors remain inside this module.</summary>
public abstract class OnnxVisionModel<TInput, TOutput> : VisionModelBase<TInput, TOutput>
{
    protected OnnxVisionModel(OnnxVisionModelConfig config)
        : base(config.ModelId, VisionBackendIds.Onnx)
    {
        config.Validate();
        Config = config;
    }

    protected OnnxVisionModelConfig Config { get; }
}
