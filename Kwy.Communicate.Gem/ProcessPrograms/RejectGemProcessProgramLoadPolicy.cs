namespace Kwy.Communicate.Gem;

/// <summary>安全默认策略：未接入 Recipe Service 时拒绝接收 Process Program。</summary>
public sealed class RejectGemProcessProgramLoadPolicy : IGemProcessProgramLoadPolicy
{
    public Task<GemProcessProgramLoadGrant> EvaluateAsync(
        GemProcessProgramLoadInquire request,
        GemCommunicationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GemProcessProgramLoadGrant.WillNotAccept);
    }
}
