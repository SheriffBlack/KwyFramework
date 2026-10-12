using KwyPecvd.Device.RfMatcher;

namespace KwyPecvd.RT.Requests;

public sealed record SetRfMatcherModeRequest
{
    public required string MatcherId { get; init; }

    public RfMatcherMode Mode { get; init; }
}