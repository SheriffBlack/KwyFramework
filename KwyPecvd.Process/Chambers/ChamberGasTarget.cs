namespace KwyPecvd.Process.Chambers;

/// <summary>一次腔体气体准备所需的单路目标。</summary>
public sealed record ChamberGasTarget
{
    public required string GasName { get; init; }

    public double Target { get; init; }

    public TimeSpan RampDuration { get; init; }
}