using System.Collections.Concurrent;
using System.Text.Json;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class InMemoryNamedPositionRepository : INamedPositionRepository
{
    private readonly ConcurrentDictionary<string, NamedPositionSet> positions = new(StringComparer.OrdinalIgnoreCase);

    public Task<NamedPositionSet?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(name);
        positions.TryGetValue(name, out var position);
        return Task.FromResult(position);
    }

    public Task<IReadOnlyList<NamedPositionSet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<NamedPositionSet>>(
            positions.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public Task SaveAsync(NamedPositionSet position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(position);
        positions[position.Name] = position;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(name);
        return Task.FromResult(positions.TryRemove(name, out _));
    }

    internal static void Validate(NamedPositionSet position)
    {
        ArgumentNullException.ThrowIfNull(position);
        ValidateName(position.Name);
        if (position.Positions.Count == 0)
        {
            throw new ArgumentException("A named position must contain at least one axis.", nameof(position));
        }

        if (position.Positions.Keys.Any(axis => axis < 1)
            || position.Positions.Values.Any(value => !double.IsFinite(value)))
        {
            throw new ArgumentException("Named position contains an invalid axis or position.", nameof(position));
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Position name cannot be empty.", nameof(name));
        }
    }
}

public sealed class JsonNamedPositionRepository : INamedPositionRepository
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public JsonNamedPositionRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        this.filePath = Path.GetFullPath(filePath);
    }

    public async Task<NamedPositionSet?> GetAsync(string name, CancellationToken cancellationToken = default)
        => (await GetAllAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<NamedPositionSet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAllCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(NamedPositionSet position, CancellationToken cancellationToken = default)
    {
        InMemoryNamedPositionRepository.Validate(position);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            int index = items.FindIndex(item => string.Equals(item.Name, position.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                items[index] = position;
            }
            else
            {
                items.Add(position);
            }

            await WriteAllCoreAsync(items, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            int removed = items.RemoveAll(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAllCoreAsync(items, cancellationToken).ConfigureAwait(false);
            }

            return removed > 0;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<NamedPositionSet>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<NamedPositionSet>();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<NamedPositionSet>>(stream, serializerOptions, cancellationToken).ConfigureAwait(false)
            ?? new List<NamedPositionSet>();
    }

    private async Task WriteAllCoreAsync(IReadOnlyCollection<NamedPositionSet> items, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, items, serializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, filePath, true);
    }
}

public sealed class NamedPositionMotionService : INamedPositionMotionService
{
    private readonly INamedPositionRepository repository;
    private readonly IAxisMotionExecutor executor;
    private readonly IMotionStateProvider stateProvider;
    private readonly IMotionSafetyGuard safetyGuard;

    public NamedPositionMotionService(
        INamedPositionRepository repository,
        IAxisMotionExecutor executor,
        IMotionStateProvider stateProvider,
        IMotionSafetyGuard safetyGuard)
    {
        this.repository = repository;
        this.executor = executor;
        this.stateProvider = stateProvider;
        this.safetyGuard = safetyGuard;
    }

    public async Task MoveToAsync(string name, MotionProfile profile, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        NamedPositionSet target = await repository.GetAsync(name, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Named position not found: {name}");

        var options = new MotionExecutionOptions
        {
            PositionTolerance = 0.01,
            Timeout = timeout
        };

        foreach ((short axis, double position) in target.Positions)
        {
            MotionAxisSnapshot snapshot = stateProvider.GetAxisSnapshot(axis);
            safetyGuard.ValidateAndThrow(new(
                axis,
                MotionRequestKind.Absolute,
                position,
                Math.Sign(position - snapshot.Position)));
        }

        var tasks = new Task<MotionCompletionResult>[target.Positions.Count];
        int index = 0;
        foreach ((short axis, double position) in target.Positions)
        {
            tasks[index++] = executor.MoveAbsAsync(axis, position, profile, options, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
