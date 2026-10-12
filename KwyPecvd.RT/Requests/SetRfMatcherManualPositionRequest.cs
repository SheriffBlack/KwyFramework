namespace KwyPecvd.RT.Requests;

public sealed record SetRfMatcherManualPositionRequest
{
    public required string MatcherId { get; init; }

    public double TunePosition { get; init; }

    public double LoadPosition { get; init; }
}
