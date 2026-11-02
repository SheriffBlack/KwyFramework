namespace Kwy.Communicate.Gem;

/// <summary>决定 Equipment 是否允许 Host 继续发送 S7F3。</summary>
public interface IGemProcessProgramLoadPolicy
{
    Task<GemProcessProgramLoadGrant> EvaluateAsync(
        GemProcessProgramLoadInquire request,
        GemCommunicationContext context,
        CancellationToken cancellationToken = default);
}
