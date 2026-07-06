using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Runtime;

namespace Kwy.Vision.DeepLearning.Halcon;

public sealed class HalconDeepLearningModelConfig
{
    public required string ModelId { get; init; }

    public required string ModelPath { get; init; }

    public string Device { get; init; } = "auto";

    public int BatchSize { get; init; } = 1;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelPath);
        if (BatchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize));
        }
    }
}

/// <summary>Base class for HALCON Deep Learning models with isolated HALCON configuration.</summary>
public abstract class HalconDeepLearningModel<TInput, TOutput> : VisionModelBase<TInput, TOutput>
{
    protected HalconDeepLearningModel(HalconDeepLearningModelConfig config)
        : base(config.ModelId, VisionBackendIds.HalconDeepLearning)
    {
        config.Validate();
        Config = config;
    }

    protected HalconDeepLearningModelConfig Config { get; }
}
