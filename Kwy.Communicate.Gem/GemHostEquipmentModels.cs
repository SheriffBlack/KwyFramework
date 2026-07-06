namespace Kwy.Communicate.Gem;

public sealed record GemEndpoint(
    GemHostRole Role,
    string Name,
    string? Model = null,
    string? SoftwareRevision = null);

public sealed record GemCommunicationContext(
    GemEndpoint Local,
    GemEndpoint Remote,
    GemCommunicationState CommunicationState,
    GemControlState ControlState);
