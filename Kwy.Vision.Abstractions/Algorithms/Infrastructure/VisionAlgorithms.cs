using Kwy.Vision.Abstractions.Results;

namespace Kwy.Vision.Abstractions.Algorithms;

public interface IVisionAlgorithm
{
    string AlgorithmId { get; }

    string BackendId { get; }

    Type RequestType { get; }

    Type ResultType { get; }
}

public interface IVisionAlgorithm<in TRequest, TResult> : IVisionAlgorithm
{
    ValueTask<VisionExecutionResult<TResult>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}

public interface IVisionAlgorithmRegistry
{
    IReadOnlyCollection<IVisionAlgorithm> Algorithms { get; }

    IVisionAlgorithm<TRequest, TResult> GetRequired<TRequest, TResult>(
        string algorithmId,
        string? backendId = null);
}

public sealed class VisionAlgorithmRegistry : IVisionAlgorithmRegistry
{
    private readonly IReadOnlyList<IVisionAlgorithm> algorithms;

    public VisionAlgorithmRegistry(IEnumerable<IVisionAlgorithm> algorithms)
    {
        ArgumentNullException.ThrowIfNull(algorithms);
        this.algorithms = algorithms.ToArray();
        ValidateDuplicates(this.algorithms);
    }

    public IReadOnlyCollection<IVisionAlgorithm> Algorithms => algorithms;

    public IVisionAlgorithm<TRequest, TResult> GetRequired<TRequest, TResult>(
        string algorithmId,
        string? backendId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmId);

        IVisionAlgorithm[] candidates = algorithms
            .Where(item => string.Equals(item.AlgorithmId, algorithmId, StringComparison.OrdinalIgnoreCase)
                && (backendId == null
                    || string.Equals(item.BackendId, backendId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new KeyNotFoundException(
                $"Vision algorithm '{algorithmId}' for backend '{backendId ?? "<any>"}' is not registered.");
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple implementations of vision algorithm '{algorithmId}' were found. Specify backendId explicitly.");
        }

        return candidates[0] as IVisionAlgorithm<TRequest, TResult>
            ?? throw new InvalidOperationException(
                $"Vision algorithm '{algorithmId}' expects {candidates[0].RequestType.Name} -> {candidates[0].ResultType.Name}, " +
                $"not {typeof(TRequest).Name} -> {typeof(TResult).Name}.");
    }

    private static void ValidateDuplicates(IEnumerable<IVisionAlgorithm> algorithms)
    {
        string? duplicate = algorithms
            .GroupBy(item => (item.AlgorithmId, item.BackendId), StringTupleComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key.AlgorithmId;

        if (duplicate != null)
        {
            throw new InvalidOperationException($"A vision algorithm registration for '{duplicate}' is duplicated.");
        }
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string AlgorithmId, string BackendId)>
    {
        public static StringTupleComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals((string AlgorithmId, string BackendId) x, (string AlgorithmId, string BackendId) y)
            => string.Equals(x.AlgorithmId, y.AlgorithmId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.BackendId, y.BackendId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string AlgorithmId, string BackendId) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AlgorithmId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.BackendId));
    }
}

public abstract class VisionAlgorithmBase<TRequest, TResult> : IVisionAlgorithm<TRequest, TResult>
{
    protected VisionAlgorithmBase(string algorithmId, string backendId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        AlgorithmId = algorithmId;
        BackendId = backendId;
    }

    public string AlgorithmId { get; }

    public string BackendId { get; }

    public Type RequestType => typeof(TRequest);

    public Type ResultType => typeof(TResult);

    public abstract ValueTask<VisionExecutionResult<TResult>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
