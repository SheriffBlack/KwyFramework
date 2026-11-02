namespace Kwy.Communicate.Gem;

public sealed partial class GemEquipmentSession
{
    public async Task<GemProcessProgramSaveResult> SaveProcessProgramAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processProgram);
        GemProcessProgramSaveResult result = await processProgramRepository.SaveAsync(
            processProgram, options, cancellationToken).ConfigureAwait(false);
        registry.RecordProcessProgramChange(
            processProgram.Ppid,
            result.Succeeded ? "Save" : "SaveRejected",
            reason: result.Message);
        return result;
    }

    public Task<GemProcessProgram?> FindProcessProgramAsync(string ppid, CancellationToken cancellationToken = default)
        => processProgramRepository.FindAsync(ppid, cancellationToken);

    public Task<IReadOnlyList<string>> ListProcessProgramIdsAsync(CancellationToken cancellationToken = default)
        => processProgramRepository.ListPpidsAsync(cancellationToken);

    public async Task<GemProcessProgramDeleteResult> DeleteProcessProgramAsync(
        string ppid,
        CancellationToken cancellationToken = default)
    {
        GemProcessProgramDeleteResult result = await processProgramRepository.DeleteAsync(ppid, cancellationToken)
            .ConfigureAwait(false);
        registry.RecordProcessProgramChange(
            ppid,
            result.Succeeded ? "Delete" : "DeleteRejected",
            reason: result.Message);
        return result;
    }
}
