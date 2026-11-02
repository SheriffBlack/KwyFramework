namespace Kwy.Communicate.Gem;

/// <summary>基于 Repository 的容量与重复 PPID 接收策略。</summary>
public sealed class RepositoryGemProcessProgramLoadPolicy : IGemProcessProgramLoadPolicy
{
    private readonly IGemProcessProgramRepository repository;
    private readonly uint maximumBodyLength;
    private readonly bool allowOverwrite;

    public RepositoryGemProcessProgramLoadPolicy(
        IGemProcessProgramRepository repository,
        uint maximumBodyLength,
        bool allowOverwrite = false)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        if (maximumBodyLength == 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBodyLength));
        this.maximumBodyLength = maximumBodyLength;
        this.allowOverwrite = allowOverwrite;
    }

    public async Task<GemProcessProgramLoadGrant> EvaluateAsync(
        GemProcessProgramLoadInquire request,
        GemCommunicationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Ppid))
            return GemProcessProgramLoadGrant.InvalidPpid;
        if (request.Length > maximumBodyLength)
            return GemProcessProgramLoadGrant.NoSpace;

        using GemProcessProgram? existing = await repository.FindAsync(request.Ppid, cancellationToken)
            .ConfigureAwait(false);
        return existing is not null && !allowOverwrite
            ? GemProcessProgramLoadGrant.AlreadyHave
            : GemProcessProgramLoadGrant.Granted;
    }
}
