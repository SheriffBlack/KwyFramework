using System.Collections.Concurrent;

namespace Kwy.Communicate.Gem;

/// <summary>
/// 定义 GEM Stream 7 Process Program 与设备配方系统之间的存储边界。
/// </summary>
/// <remarks>
/// 本接口处理 PPID/PPBODY，不表示通信库拥有设备完整的 Recipe 领域模型。
/// 生产项目通常通过适配器将其连接到现有 Recipe Service。
/// </remarks>
public interface IGemProcessProgramRepository
{
    Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default);

    Task<GemProcessProgram?> FindAsync(string ppid, CancellationToken cancellationToken = default);

    Task<GemProcessProgramSaveResult> SaveAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<GemProcessProgramDeleteResult> DeleteAsync(
        string ppid,
        CancellationToken cancellationToken = default);
}

/// <summary>默认拒绝 Process Program 修改，避免未配置持久化时误报保存成功。</summary>
public sealed class UnsupportedGemProcessProgramRepository : IGemProcessProgramRepository
{
    public Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<GemProcessProgram?> FindAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<GemProcessProgram?>(null);
    }

    public Task<GemProcessProgramSaveResult> SaveAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processProgram);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GemProcessProgramSaveResult(
            GemProcessProgramSaveStatus.Rejected,
            "Process Program storage is not configured."));
    }

    public Task<GemProcessProgramDeleteResult> DeleteAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GemProcessProgramDeleteResult(
            GemProcessProgramDeleteStatus.Rejected,
            "Process Program storage is not configured."));
    }
}

/// <summary>提供适合学习、测试和 Simulator 的内存 Process Program 存储。</summary>
public sealed class InMemoryGemProcessProgramRepository : IGemProcessProgramRepository
{
    private readonly ConcurrentDictionary<string, GemProcessProgram> programs =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> result = programs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult(result);
    }

    public Task<GemProcessProgram?> FindAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        programs.TryGetValue(ppid, out GemProcessProgram? processProgram);
        return Task.FromResult(processProgram);
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

        if (!options.Overwrite && !programs.TryAdd(processProgram.Ppid, processProgram))
        {
            return Task.FromResult(new GemProcessProgramSaveResult(
                GemProcessProgramSaveStatus.AlreadyExists,
                $"Process Program '{processProgram.Ppid}' already exists."));
        }

        if (options.Overwrite)
        {
            programs[processProgram.Ppid] = processProgram;
        }

        return Task.FromResult(new GemProcessProgramSaveResult(GemProcessProgramSaveStatus.Saved));
    }

    public Task<GemProcessProgramDeleteResult> DeleteAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        bool removed = programs.TryRemove(ppid, out _);
        return Task.FromResult(new GemProcessProgramDeleteResult(
            removed ? GemProcessProgramDeleteStatus.Deleted : GemProcessProgramDeleteStatus.NotFound));
    }
}
