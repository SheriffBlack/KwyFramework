using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.Abstractions.Images;

namespace Kwy.Vision.Abstractions.DeepLearning;

public enum VisionModelState
{
    Unloaded,
    Loading,
    Loaded,
    Faulted,
    Disposed
}

public interface IVisionModel : IDisposable, IAsyncDisposable
{
    string ModelId { get; }

    string BackendId { get; }

    Type InputType { get; }

    Type OutputType { get; }

    VisionModelState State { get; }

    ValueTask LoadAsync(CancellationToken cancellationToken = default);

    ValueTask UnloadAsync(CancellationToken cancellationToken = default);
}

public interface IVisionModel<in TInput, TOutput> : IVisionModel
{
    ValueTask<TOutput> PredictAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}

public interface IVisionModelRegistry
{
    IReadOnlyCollection<IVisionModel> Models { get; }

    IVisionModel<TInput, TOutput> GetRequired<TInput, TOutput>(string modelId);
}

public sealed class VisionModelRegistry : IVisionModelRegistry
{
    private readonly IReadOnlyDictionary<string, IVisionModel> models;

    public VisionModelRegistry(IEnumerable<IVisionModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        var byId = new Dictionary<string, IVisionModel>(StringComparer.OrdinalIgnoreCase);
        foreach (IVisionModel model in models)
        {
            if (!byId.TryAdd(model.ModelId, model))
            {
                throw new InvalidOperationException($"Vision model '{model.ModelId}' is already registered.");
            }
        }

        this.models = byId;
        Models = byId.Values.ToArray();
    }

    public IReadOnlyCollection<IVisionModel> Models { get; }

    public IVisionModel<TInput, TOutput> GetRequired<TInput, TOutput>(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        if (!models.TryGetValue(modelId, out IVisionModel? model))
        {
            throw new KeyNotFoundException($"Vision model '{modelId}' is not registered.");
        }

        return model as IVisionModel<TInput, TOutput>
            ?? throw new InvalidOperationException(
                $"Vision model '{modelId}' expects {model.InputType.Name} -> {model.OutputType.Name}, " +
                $"not {typeof(TInput).Name} -> {typeof(TOutput).Name}.");
    }
}

public abstract class VisionModelBase<TInput, TOutput> : IVisionModel<TInput, TOutput>
{
    private readonly SemaphoreSlim lifecycleSemaphore = new(1, 1);
    private bool disposed;

    protected VisionModelBase(string modelId, string backendId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        ModelId = modelId;
        BackendId = backendId;
    }

    public string ModelId { get; }

    public string BackendId { get; }

    public Type InputType => typeof(TInput);

    public Type OutputType => typeof(TOutput);

    public VisionModelState State { get; private set; }

    public async ValueTask LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == VisionModelState.Loaded)
            {
                return;
            }

            State = VisionModelState.Loading;
            try
            {
                await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
                State = VisionModelState.Loaded;
            }
            catch
            {
                State = VisionModelState.Faulted;
                throw;
            }
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async ValueTask UnloadAsync(CancellationToken cancellationToken = default)
    {
        await lifecycleSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is VisionModelState.Unloaded or VisionModelState.Disposed)
            {
                return;
            }

            await UnloadCoreAsync(cancellationToken).ConfigureAwait(false);
            State = VisionModelState.Unloaded;
        }
        finally
        {
            lifecycleSemaphore.Release();
        }
    }

    public async ValueTask<TOutput> PredictAsync(TInput input, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State != VisionModelState.Loaded)
        {
            throw new InvalidOperationException($"Vision model '{ModelId}' is not loaded.");
        }

        return await PredictCoreAsync(input, cancellationToken).ConfigureAwait(false);
    }

    protected abstract ValueTask LoadCoreAsync(CancellationToken cancellationToken);

    protected abstract ValueTask UnloadCoreAsync(CancellationToken cancellationToken);

    protected abstract ValueTask<TOutput> PredictCoreAsync(TInput input, CancellationToken cancellationToken);

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await UnloadAsync().ConfigureAwait(false);
        disposed = true;
        State = VisionModelState.Disposed;
        lifecycleSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed record ClassificationScore(string Label, double Confidence);

public sealed record ClassificationResult(IReadOnlyList<ClassificationScore> Scores)
{
    public ClassificationScore? Best => Scores.OrderByDescending(item => item.Confidence).FirstOrDefault();
}

public sealed record ObjectDetection(
    string Label,
    double Confidence,
    VisionRectangle Bounds);

public sealed record ObjectDetectionResult(IReadOnlyList<ObjectDetection> Detections);

public sealed record SegmentationResult(
    IVisionImage Mask,
    IReadOnlyDictionary<int, string> Classes);

public sealed record AnomalyResult(
    double Score,
    bool IsAnomaly,
    IVisionImage? ScoreMap = null);
