namespace Kwy.Communicate.Gem;

/// <summary>集中描述 Host Primary Message 可以在哪些会话状态下执行。</summary>
public sealed record GemSessionPolicy(
    bool RequireCommunicating = true,
    IReadOnlySet<GemControlState>? AllowedControlStates = null)
{
    public GemValidationResult Validate(GemCommunicationContext context)
    {
        if (RequireCommunicating && context.CommunicationState != GemCommunicationState.Communicating)
            return GemValidationResult.Failure("GEM session is not communicating.");
        if (AllowedControlStates is { Count: > 0 } states && !states.Contains(context.ControlState))
            return GemValidationResult.Failure($"Control state {context.ControlState} is not allowed.");
        return GemValidationResult.Success;
    }
}
