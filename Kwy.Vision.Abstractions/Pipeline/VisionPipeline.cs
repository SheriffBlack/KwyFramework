namespace Kwy.Vision.Abstractions.Pipeline;

public interface IVisionPipeline<in TInput, TOutput>
{
    string PipelineId { get; }

    ValueTask<TOutput> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base type for business pipelines with strongly typed input and output.
/// Intermediate state remains private to the concrete pipeline.
/// </summary>
public abstract class VisionPipelineBase<TInput, TOutput> : IVisionPipeline<TInput, TOutput>
{
    protected VisionPipelineBase(string pipelineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineId);
        PipelineId = pipelineId;
    }

    public string PipelineId { get; }

    public abstract ValueTask<TOutput> ExecuteAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
