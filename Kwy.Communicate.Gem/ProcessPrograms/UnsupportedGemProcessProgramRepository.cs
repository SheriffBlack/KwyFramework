namespace Kwy.Communicate.Gem;

/// <summary>默认拒绝 Process Program 操作，避免未配置存储时误报成功。</summary>
public sealed class UnsupportedGemProcessProgramRepository : IGemProcessProgramRepository
{
    public Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<GemProcessProgram?> FindAsync(string ppid, CancellationToken cancellationToken = default)
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

    public Task<GemProcessProgramDeleteResult> DeleteAsync(string ppid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppid);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GemProcessProgramDeleteResult(
            GemProcessProgramDeleteStatus.Rejected,
            "Process Program storage is not configured."));
    }
}
