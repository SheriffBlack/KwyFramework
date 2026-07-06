using HalconDotNet;

namespace Kwy.Vision.Halcon.Models;

public sealed class HalconShapeModelConfig
{
    public required string TemplateId { get; init; }

    public required string ModelPath { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TemplateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ModelPath);
    }
}

public interface IHalconShapeModelRepository : IDisposable, IAsyncDisposable
{
    IReadOnlyCollection<string> TemplateIds { get; }

    bool Contains(string templateId);

    void Load(HalconShapeModelConfig config, bool replace = false);

    bool Remove(string templateId);
}

public sealed class HalconShapeModelRepository : IHalconShapeModelRepository
{
    private readonly Dictionary<string, ModelEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();
    private bool disposed;

    public IReadOnlyCollection<string> TemplateIds
    {
        get
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                return entries.Keys.ToArray();
            }
        }
    }

    public bool Contains(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        lock (syncRoot)
        {
            ThrowIfDisposed();
            return entries.ContainsKey(templateId);
        }
    }

    public void Load(HalconShapeModelConfig config, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        lock (syncRoot)
        {
            ThrowIfDisposed();
            if (entries.ContainsKey(config.TemplateId) && !replace)
            {
                throw new InvalidOperationException(
                    $"HALCON shape template '{config.TemplateId}' is already loaded.");
            }
        }

        var newEntry = new ModelEntry(new HShapeModel(config.ModelPath));
        ModelEntry? oldEntry = null;
        try
        {
            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (entries.TryGetValue(config.TemplateId, out oldEntry) && !replace)
                {
                    throw new InvalidOperationException(
                        $"HALCON shape template '{config.TemplateId}' is already loaded.");
                }

                entries[config.TemplateId] = newEntry;
                newEntry = null!;
            }
        }
        finally
        {
            newEntry?.Dispose();
        }

        oldEntry?.Dispose();
    }

    public bool Remove(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ModelEntry? entry;
        lock (syncRoot)
        {
            ThrowIfDisposed();
            if (!entries.Remove(templateId, out entry))
            {
                return false;
            }
        }

        entry.Dispose();
        return true;
    }

    internal async ValueTask<ModelLease> AcquireAsync(
        string templateId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ModelEntry entry;
        lock (syncRoot)
        {
            ThrowIfDisposed();
            entry = entries.TryGetValue(templateId, out ModelEntry? found)
                ? found
                : throw new KeyNotFoundException(
                    $"HALCON shape template '{templateId}' is not loaded.");
            entry.AddReference();
        }

        try
        {
            await entry.ExecutionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ModelLease(entry);
        }
        catch
        {
            entry.ReleaseReference();
            throw;
        }
    }

    public void Dispose()
    {
        List<ModelEntry> snapshot;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            snapshot = entries.Values.ToList();
            entries.Clear();
        }

        foreach (ModelEntry entry in snapshot)
        {
            entry.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(disposed, this);

    internal sealed class ModelLease : IDisposable
    {
        private ModelEntry? entry;

        public ModelLease(ModelEntry entry)
        {
            this.entry = entry;
        }

        public HShapeModel Model
            => entry?.Model ?? throw new ObjectDisposedException(nameof(ModelLease));

        public void Dispose()
        {
            ModelEntry? current = Interlocked.Exchange(ref entry, null);
            if (current == null)
            {
                return;
            }

            current.ExecutionLock.Release();
            current.ReleaseReference();
        }
    }

    internal sealed class ModelEntry : IDisposable
    {
        private readonly ManualResetEventSlim referencesReleased = new(initialState: true);
        private int referenceCount;
        private bool disposed;

        public ModelEntry(HShapeModel model)
        {
            Model = model;
        }

        public HShapeModel Model { get; }

        public SemaphoreSlim ExecutionLock { get; } = new(1, 1);

        public void AddReference()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ModelEntry));
            }

            referencesReleased.Reset();
            Interlocked.Increment(ref referenceCount);
        }

        public void ReleaseReference()
        {
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                referencesReleased.Set();
            }
        }

        public void Dispose()
        {
            disposed = true;
            referencesReleased.Wait();
            Model.Dispose();
            ExecutionLock.Dispose();
            referencesReleased.Dispose();
        }
    }
}
