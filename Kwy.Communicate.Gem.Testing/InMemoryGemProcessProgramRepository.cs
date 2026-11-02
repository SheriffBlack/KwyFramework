using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem.Testing;

/// <summary>提供适合测试和 Host Simulator 的内存 Process Program 存储。</summary>
/// <remarks>数据不会持久化，不得作为生产设备的 Recipe 存储。</remarks>
public sealed class InMemoryGemProcessProgramRepository : IGemProcessProgramRepository, IDisposable
{
    private readonly ConcurrentDictionary<string, GemProcessProgram> programs =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> result = programs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult(result);
    }

    public Task<GemProcessProgram?> FindAsync(string ppid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        programs.TryGetValue(ppid, out GemProcessProgram? processProgram);
        return Task.FromResult(processProgram is null ? null : Clone(processProgram));
    }

    public Task<GemProcessProgramSaveResult> SaveAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processProgram);
        ArgumentException.ThrowIfNullOrWhiteSpace(processProgram.Ppid);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new GemProcessProgramSaveOptions();

        GemProcessProgram ownedCopy = Clone(processProgram);
        if (!options.Overwrite && !programs.TryAdd(processProgram.Ppid, ownedCopy))
        {
            ownedCopy.Dispose();
            return Task.FromResult(new GemProcessProgramSaveResult(
                GemProcessProgramSaveStatus.AlreadyExists,
                $"Process Program '{processProgram.Ppid}' already exists."));
        }

        if (options.Overwrite)
        {
            programs.AddOrUpdate(
                processProgram.Ppid,
                ownedCopy,
                (_, previous) =>
                {
                    previous.Dispose();
                    return ownedCopy;
                });
        }

        return Task.FromResult(new GemProcessProgramSaveResult(GemProcessProgramSaveStatus.Saved));
    }

    public Task<GemProcessProgramDeleteResult> DeleteAsync(string ppid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        bool removed = programs.TryRemove(ppid, out GemProcessProgram? removedProgram);
        removedProgram?.Dispose();
        return Task.FromResult(new GemProcessProgramDeleteResult(
            removed ? GemProcessProgramDeleteStatus.Deleted : GemProcessProgramDeleteStatus.NotFound));
    }

    public void Dispose()
    {
        foreach ((string key, GemProcessProgram _) in programs)
        {
            if (programs.TryRemove(key, out GemProcessProgram? removed))
                removed.Dispose();
        }
    }

    private static GemProcessProgram Clone(GemProcessProgram processProgram)
        => processProgram with { Body = GemItemCloner.Clone(processProgram.Body) };
}
