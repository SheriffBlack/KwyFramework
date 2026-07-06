namespace Kwy.Vision.Abstractions.Runtime;

public static class VisionBackendIds
{
    public const string Halcon = "Halcon";
    public const string OpenCv = "OpenCV";
    public const string HalconDeepLearning = "Halcon.DeepLearning";
    public const string Onnx = "Onnx";
}

public sealed record VisionBackendDescriptor(
    string BackendId,
    string DisplayName,
    bool SupportsTraditionalVision,
    bool SupportsDeepLearning);

public interface IVisionBackendCatalog
{
    IReadOnlyCollection<VisionBackendDescriptor> Backends { get; }

    VisionBackendDescriptor GetRequired(string backendId);
}

public sealed class VisionBackendCatalog : IVisionBackendCatalog
{
    private readonly IReadOnlyDictionary<string, VisionBackendDescriptor> backends;

    public VisionBackendCatalog(IEnumerable<VisionBackendDescriptor> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);
        var byId = new Dictionary<string, VisionBackendDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (VisionBackendDescriptor backend in backends)
        {
            if (!byId.TryAdd(backend.BackendId, backend))
            {
                throw new InvalidOperationException($"Vision backend '{backend.BackendId}' is already registered.");
            }
        }

        this.backends = byId;
        Backends = byId.Values.ToArray();
    }

    public IReadOnlyCollection<VisionBackendDescriptor> Backends { get; }

    public VisionBackendDescriptor GetRequired(string backendId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendId);
        return backends.TryGetValue(backendId, out VisionBackendDescriptor? backend)
            ? backend
            : throw new KeyNotFoundException($"Vision backend '{backendId}' is not registered.");
    }
}
